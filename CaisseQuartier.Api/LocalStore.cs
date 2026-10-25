using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace CaisseQuartier.Api;

public sealed class LocalStore
{
    private const int PasswordIterations = 210_000;
    private readonly string _dataDirectory = Environment.GetEnvironmentVariable("CAISSEQUARTIER_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaisseQuartier");

    public string UploadDirectory => Path.Combine(_dataDirectory, "uploads");

    private string ConnectionString =>
        new SqliteConnectionStringBuilder { DataSource = Path.Combine(_dataDirectory, "caissequartier.db") }.ToString();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(UploadDirectory);
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS Associations (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Purpose TEXT NOT NULL,
                MonthlyGoal TEXT NOT NULL,
                JoinCode TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Members (
                Id TEXT PRIMARY KEY,
                AssociationId TEXT NOT NULL REFERENCES Associations(Id),
                FullName TEXT NOT NULL,
                Contact TEXT NOT NULL,
                PasswordSalt TEXT,
                PasswordHash TEXT,
                Phone TEXT,
                Email TEXT,
                Neighborhood TEXT,
                Occupation TEXT,
                JoinedOn TEXT NOT NULL,
                MonthlyContribution TEXT NOT NULL DEFAULT '0',
                PhotoFile TEXT,
                Role TEXT NOT NULL DEFAULT 'member',
                CreatedAt TEXT NOT NULL,
                UNIQUE (AssociationId, Contact)
            );
            CREATE TABLE IF NOT EXISTS Sessions (
                TokenHash TEXT PRIMARY KEY,
                MemberId TEXT NOT NULL REFERENCES Members(Id) ON DELETE CASCADE,
                ExpiresAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                AssociationId TEXT NOT NULL REFERENCES Associations(Id),
                MemberId TEXT NOT NULL REFERENCES Members(Id),
                CreatedBy TEXT NOT NULL REFERENCES Members(Id),
                Type TEXT NOT NULL CHECK(Type IN ('contribution', 'expense')),
                Title TEXT NOT NULL,
                Description TEXT,
                Amount TEXT NOT NULL,
                Date TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS AuditEvents (
                Id TEXT PRIMARY KEY,
                AssociationId TEXT NOT NULL REFERENCES Associations(Id),
                MemberId TEXT NOT NULL REFERENCES Members(Id),
                Type TEXT NOT NULL,
                Summary TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Members_Association ON Members(AssociationId, FullName);
            CREATE INDEX IF NOT EXISTS IX_Transactions_AssociationDate ON Transactions(AssociationId, Date);
            CREATE INDEX IF NOT EXISTS IX_Sessions_ExpiresAt ON Sessions(ExpiresAt);
            CREATE INDEX IF NOT EXISTS IX_AuditEvents_AssociationDate ON AuditEvents(AssociationId, CreatedAt);
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        await using var connection = await OpenAsync();
        await using var transaction = connection.BeginTransaction();
        var contact = NormalizeContact(request.Contact);

        Guid associationId;
        string role;
        if (!string.IsNullOrWhiteSpace(request.JoinCode))
        {
            var association = await ReadAssociationByCodeAsync(connection, transaction, request.JoinCode.Trim());
            if (association is null)
                throw new InvalidOperationException("Code d'association invalide. Demandez le code à un administrateur.");
            associationId = association.Id;
            role = "member";
        }
        else
        {
            associationId = Guid.NewGuid();
            role = "admin";
            await using var associationCommand = connection.CreateCommand();
            associationCommand.Transaction = transaction;
            associationCommand.CommandText = """
                INSERT INTO Associations (Id, Name, Purpose, MonthlyGoal, JoinCode, CreatedAt)
                VALUES ($id, $name, $purpose, $goal, $code, $created);
                """;
            associationCommand.Parameters.AddWithValue("$id", associationId.ToString());
            associationCommand.Parameters.AddWithValue("$name", request.AssociationName!.Trim());
            associationCommand.Parameters.AddWithValue("$purpose", request.Purpose!.Trim());
            associationCommand.Parameters.AddWithValue("$goal", Format(request.MonthlyGoal));
            associationCommand.Parameters.AddWithValue("$code", CreateJoinCode());
            associationCommand.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await associationCommand.ExecuteNonQueryAsync();
        }

        if (await RegisteredContactExistsAsync(connection, transaction, contact))
            throw new InvalidOperationException("Ce téléphone ou cet email possède déjà un compte.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(request.Password, salt);
        var pendingMemberId = string.IsNullOrWhiteSpace(request.JoinCode)
            ? null
            : await FindPendingMemberAsync(connection, transaction, associationId, contact);
        Guid memberId = pendingMemberId ?? Guid.NewGuid();
        if (pendingMemberId is not null)
        {
            await using var claimCommand = connection.CreateCommand();
            claimCommand.Transaction = transaction;
            claimCommand.CommandText = """
                UPDATE Members SET FullName=$name, PasswordSalt=$salt, PasswordHash=$hash
                WHERE Id=$id AND AssociationId=$association AND PasswordHash IS NULL;
                """;
            claimCommand.Parameters.AddWithValue("$id", memberId.ToString());
            claimCommand.Parameters.AddWithValue("$association", associationId.ToString());
            claimCommand.Parameters.AddWithValue("$name", request.FullName.Trim());
            claimCommand.Parameters.AddWithValue("$salt", Convert.ToBase64String(salt));
            claimCommand.Parameters.AddWithValue("$hash", Convert.ToBase64String(hash));
            if (await claimCommand.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Ce profil ne peut pas être activé. Demandez l'aide de l'administrateur.");
        }
        else
        {
            await using var memberCommand = connection.CreateCommand();
            memberCommand.Transaction = transaction;
            memberCommand.CommandText = """
                INSERT INTO Members
                  (Id, AssociationId, FullName, Contact, PasswordSalt, PasswordHash, JoinedOn,
                   MonthlyContribution, Role, CreatedAt)
                VALUES ($id, $association, $name, $contact, $salt, $hash, $joined, '0', $role, $created);
                """;
            memberCommand.Parameters.AddWithValue("$id", memberId.ToString());
            memberCommand.Parameters.AddWithValue("$association", associationId.ToString());
            memberCommand.Parameters.AddWithValue("$name", request.FullName.Trim());
            memberCommand.Parameters.AddWithValue("$contact", contact);
            memberCommand.Parameters.AddWithValue("$salt", Convert.ToBase64String(salt));
            memberCommand.Parameters.AddWithValue("$hash", Convert.ToBase64String(hash));
            memberCommand.Parameters.AddWithValue("$joined", DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"));
            memberCommand.Parameters.AddWithValue("$role", role);
            memberCommand.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await memberCommand.ExecuteNonQueryAsync();
        }

        await WriteEventAsync(connection, transaction, associationId, memberId,
            role == "admin" ? "association_created" : pendingMemberId is null ? "member_joined" : "profile_activated",
            role == "admin" ? $"Création de l'association {request.AssociationName!.Trim()}" :
            pendingMemberId is null ? $"{request.FullName.Trim()} a rejoint l'association" :
            $"{request.FullName.Trim()} a activé son profil");
        string token = await CreateSessionAsync(connection, transaction, memberId);
        await transaction.CommitAsync();
        var identity = await GetMemberIdentityAsync(associationId, memberId);
        var associationInfo = await GetAssociationAsync(associationId);
        return new AuthResponse(token, identity!, associationInfo!);
    }

    public async Task<AuthResponse?> LoginAsync(string contact, string password)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AssociationId, PasswordSalt, PasswordHash
            FROM Members WHERE Contact=$contact AND PasswordHash IS NOT NULL LIMIT 1;
            """;
        command.Parameters.AddWithValue("$contact", NormalizeContact(contact));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        Guid memberId = Guid.Parse(reader.GetString(0));
        Guid associationId = Guid.Parse(reader.GetString(1));
        var salt = Convert.FromBase64String(reader.GetString(2));
        var expected = Convert.FromBase64String(reader.GetString(3));
        var actual = HashPassword(password, salt);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return null;
        await reader.CloseAsync();

        string token = await CreateSessionAsync(connection, null, memberId);
        var identity = await GetMemberIdentityAsync(associationId, memberId);
        var association = await GetAssociationAsync(associationId);
        return identity is null || association is null ? null : new AuthResponse(token, identity, association);
    }

    public async Task<MemberIdentity?> GetSessionAsync(string token)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.Id, m.AssociationId, m.FullName, m.Contact, m.Phone, m.Email, m.Neighborhood,
                   m.Occupation, m.JoinedOn, m.MonthlyContribution, m.PhotoFile, m.Role
            FROM Sessions s JOIN Members m ON m.Id=s.MemberId
            WHERE s.TokenHash=$hash AND s.ExpiresAt > $now LIMIT 1;
            """;
        command.Parameters.AddWithValue("$hash", HashToken(token));
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadIdentity(reader) : null;
    }

    public async Task RevokeSessionAsync(string token)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Sessions WHERE TokenHash=$hash;";
        command.Parameters.AddWithValue("$hash", HashToken(token));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<AssociationInfo?> GetAssociationAsync(Guid associationId)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Purpose, MonthlyGoal, JoinCode FROM Associations WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", associationId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new AssociationInfo(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                ParseDecimal(reader.GetString(3)), reader.GetString(4))
            : null;
    }

    public async Task UpdateAssociationAsync(Guid associationId, AssociationUpdateRequest request)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Associations SET Name=$name, Purpose=$purpose, MonthlyGoal=$goal WHERE Id=$id;
            """;
        command.Parameters.AddWithValue("$id", associationId.ToString());
        command.Parameters.AddWithValue("$name", request.Name.Trim());
        command.Parameters.AddWithValue("$purpose", request.Purpose.Trim());
        command.Parameters.AddWithValue("$goal", Format(request.MonthlyGoal));
        await command.ExecuteNonQueryAsync();
    }

    public Task LogEventAsync(Guid associationId, Guid memberId, string type, string summary) =>
        WriteEventAsync(associationId, memberId, type, summary);

    public async Task<IReadOnlyList<AuditEventInfo>> GetEventsAsync(Guid associationId, DateOnly from, DateOnly to)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.Id, e.Type, e.Summary, m.FullName, e.CreatedAt
            FROM AuditEvents e
            JOIN Members m ON m.Id=e.MemberId
            WHERE e.AssociationId=$association AND substr(e.CreatedAt, 1, 10) >= $from
              AND substr(e.CreatedAt, 1, 10) <= $to
            ORDER BY e.CreatedAt DESC;
            """;
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        await using var reader = await command.ExecuteReaderAsync();
        var events = new List<AuditEventInfo>();
        while (await reader.ReadAsync())
            events.Add(new AuditEventInfo(Guid.Parse(reader.GetString(0)), reader.GetString(1),
                reader.GetString(2), reader.GetString(3),
                DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return events;
    }

    public async Task<IReadOnlyList<MemberProfile>> GetMembersAsync(Guid associationId, string? search = null)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MemberSelect +
            "\nWHERE m.AssociationId=$association AND ($search = '' OR m.FullName LIKE $pattern OR m.Contact LIKE $pattern) " +
            "GROUP BY m.Id ORDER BY m.FullName COLLATE NOCASE;";
        command.Parameters.AddWithValue("$association", associationId.ToString());
        string query = search?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("$search", query);
        command.Parameters.AddWithValue("$pattern", $"%{query}%");
        await using var reader = await command.ExecuteReaderAsync();
        var members = new List<MemberProfile>();
        while (await reader.ReadAsync()) members.Add(ReadProfile(reader));
        return members;
    }

    public async Task<MemberProfile?> GetMemberAsync(Guid associationId, Guid memberId)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MemberSelect + "\nWHERE m.AssociationId=$association AND m.Id=$id GROUP BY m.Id;";
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$id", memberId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadProfile(reader) : null;
    }

    public async Task<MemberProfile> AddMemberAsync(Guid associationId, MemberCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Contact) || request.Contact.Trim().Length < 5)
            throw new InvalidOperationException("Un téléphone ou un email est requis pour permettre au membre d'activer son profil.");
        var contact = NormalizeContact(request.Contact);
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Members (Id, AssociationId, FullName, Contact, Phone, Email, Neighborhood,
              Occupation, JoinedOn, MonthlyContribution, Role, CreatedAt)
            VALUES ($id, $association, $name, $contact, $phone, $email, $neighborhood, $occupation,
              $joined, $contribution, 'member', $created);
            """;
        var id = Guid.NewGuid();
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$name", request.FullName.Trim());
        command.Parameters.AddWithValue("$contact", contact);
        command.Parameters.AddWithValue("$phone", DbValue(request.Phone));
        command.Parameters.AddWithValue("$email", DbValue(request.Email));
        command.Parameters.AddWithValue("$neighborhood", DbValue(request.Neighborhood));
        command.Parameters.AddWithValue("$occupation", DbValue(request.Occupation));
        command.Parameters.AddWithValue("$joined", request.JoinedOn.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$contribution", Format(request.MonthlyContribution));
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Ce téléphone ou cet email est déjà enregistré.");
        }

        return (await GetMemberAsync(associationId, id))!;
    }

    public async Task<MemberProfile?> UpdateMemberAsync(
        Guid associationId, Guid memberId, Guid actorId, MemberUpdateRequest request)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Members SET FullName=$name, Phone=$phone, Email=$email, Neighborhood=$neighborhood,
              Occupation=$occupation, MonthlyContribution=$contribution
            WHERE AssociationId=$association AND Id=$id;
            """;
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$id", memberId.ToString());
        command.Parameters.AddWithValue("$name", request.FullName.Trim());
        command.Parameters.AddWithValue("$phone", DbValue(request.Phone));
        command.Parameters.AddWithValue("$email", DbValue(request.Email));
        command.Parameters.AddWithValue("$neighborhood", DbValue(request.Neighborhood));
        command.Parameters.AddWithValue("$occupation", DbValue(request.Occupation));
        command.Parameters.AddWithValue("$contribution", Format(request.MonthlyContribution));
        if (await command.ExecuteNonQueryAsync() == 0) return null;
        await WriteEventAsync(connection, null, associationId, actorId, "profile_updated",
            $"Mise à jour du profil de {request.FullName.Trim()}");
        return await GetMemberAsync(associationId, memberId);
    }

    public async Task<string?> SaveMemberPhotoAsync(
        Guid associationId, Guid memberId, Guid actorId, byte[] bytes, string extension)
    {
        var member = await GetMemberIdentityAsync(associationId, memberId);
        if (member is null) return null;
        var fileName = $"{associationId:N}-{memberId:N}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(UploadDirectory, fileName);
        await File.WriteAllBytesAsync(path, bytes);

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Members SET PhotoFile=$photo WHERE AssociationId=$association AND Id=$id;";
        command.Parameters.AddWithValue("$photo", fileName);
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$id", memberId.ToString());
        await command.ExecuteNonQueryAsync();
        await WriteEventAsync(associationId, actorId, "profile_photo_updated",
            $"Photo de profil mise à jour pour {member.FullName}");
        return $"/api/members/{memberId:D}/photo";
    }

    public async Task<(byte[] Bytes, string ContentType)?> ReadMemberPhotoAsync(Guid associationId, Guid memberId)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PhotoFile FROM Members WHERE AssociationId=$association AND Id=$id;";
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$id", memberId.ToString());
        var result = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(result)) return null;
        var extension = Path.GetExtension(result);
        if (extension is not ".jpg" and not ".png") return null;
        var path = Path.Combine(UploadDirectory, result);
        if (!File.Exists(path)) return null;
        return (await File.ReadAllBytesAsync(path), extension == ".png" ? "image/png" : "image/jpeg");
    }

    public async Task<TransactionInfo?> AddTransactionAsync(
        Guid associationId,
        Guid actorId,
        string type,
        TransactionCreateRequest request)
    {
        var memberId = type == "expense" || request.MemberId == Guid.Empty ? actorId : request.MemberId;
        var member = await GetMemberIdentityAsync(associationId, memberId);
        var actor = await GetMemberIdentityAsync(associationId, actorId);
        if (member is null || actor is null) return null;

        string title = string.IsNullOrWhiteSpace(request.Title)
            ? type == "contribution" ? "Cotisation" : "Dépense"
            : request.Title.Trim();
        var transaction = new TransactionInfo(Guid.NewGuid(), type, memberId, member.FullName, title,
            EmptyToNull(request.Description), request.Amount, request.Date, actor.FullName, DateTime.UtcNow);

        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Transactions (Id, AssociationId, MemberId, CreatedBy, Type, Title, Description,
              Amount, Date, CreatedAt)
            VALUES ($id, $association, $member, $creator, $type, $title, $description, $amount, $date, $created);
            """;
        command.Parameters.AddWithValue("$id", transaction.Id.ToString());
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$member", memberId.ToString());
        command.Parameters.AddWithValue("$creator", actorId.ToString());
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$description", DbValue(transaction.Description));
        command.Parameters.AddWithValue("$amount", Format(request.Amount));
        command.Parameters.AddWithValue("$date", request.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$created", transaction.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
        await WriteEventAsync(associationId, actorId, type,
            type == "contribution"
                ? $"{member.FullName} a versé {Format(request.Amount)} FCFA : {title}"
                : $"{actor.FullName} a enregistré une dépense de {Format(request.Amount)} FCFA : {title}");
        return transaction;
    }

    public async Task<IReadOnlyList<TransactionInfo>> GetTransactionsAsync(
        Guid associationId, DateOnly from, DateOnly to, string? type = null)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.Id, t.Type, t.MemberId, m.FullName, t.Title, t.Description, t.Amount, t.Date,
                   creator.FullName, t.CreatedAt
            FROM Transactions t
            JOIN Members m ON m.Id=t.MemberId
            JOIN Members creator ON creator.Id=t.CreatedBy
            WHERE t.AssociationId=$association AND t.Date >= $from AND t.Date <= $to
              AND ($type = '' OR t.Type=$type)
            ORDER BY t.Date DESC, t.CreatedAt DESC;
            """;
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$type", type ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<TransactionInfo>();
        while (await reader.ReadAsync()) rows.Add(ReadTransaction(reader));
        return rows;
    }

    public async Task<DashboardInfo> GetDashboardAsync(Guid associationId, DateOnly from, DateOnly to)
    {
        var association = await GetAssociationAsync(associationId)
            ?? throw new InvalidOperationException("Association introuvable.");
        var rows = await GetTransactionsAsync(associationId, from, to);
        var contributions = rows.Where(row => row.Type == "contribution").Sum(row => row.Amount);
        var expenses = rows.Where(row => row.Type == "expense").Sum(row => row.Amount);
        return new DashboardInfo(association, from, to, contributions, expenses, contributions - expenses,
            association.MonthlyGoal, rows.Take(8).ToList());
    }

    private async Task<MemberIdentity?> GetMemberIdentityAsync(Guid associationId, Guid memberId)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AssociationId, FullName, Contact, Phone, Email, Neighborhood, Occupation,
                   JoinedOn, MonthlyContribution, PhotoFile, Role
            FROM Members WHERE AssociationId=$association AND Id=$id;
            """;
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$id", memberId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadIdentity(reader) : null;
    }

    private const string MemberSelect = """
        SELECT m.Id, m.FullName, m.Contact, m.Phone, m.Email, m.Neighborhood, m.Occupation,
               m.JoinedOn, m.MonthlyContribution,
               CAST(COALESCE(SUM(CASE WHEN t.Type='contribution'
                 AND t.Date >= date('now','localtime','start of month') THEN CAST(t.Amount AS INTEGER)
                 ELSE 0 END), 0) AS INTEGER),
               m.PhotoFile, m.Role
        FROM Members m
        LEFT JOIN Transactions t ON t.MemberId=m.Id AND t.AssociationId=m.AssociationId
        """;

    private static MemberProfile ReadProfile(SqliteDataReader reader)
    {
        var expected = ParseDecimal(reader.GetString(8));
        var paid = Convert.ToDecimal(reader.GetInt64(9), CultureInfo.InvariantCulture);
        var photoFile = reader.IsDBNull(10) ? null : reader.GetString(10);
        return new MemberProfile(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
            NullableString(reader, 3), NullableString(reader, 4), NullableString(reader, 5), NullableString(reader, 6),
            DateOnly.ParseExact(reader.GetString(7), "yyyy-MM-dd", CultureInfo.InvariantCulture), expected, paid,
            Math.Max(0, expected - paid), photoFile is null ? null : $"/api/members/{reader.GetString(0)}/photo",
            reader.GetString(11), false);
    }

    private static MemberIdentity ReadIdentity(SqliteDataReader reader)
    {
        var photoFile = reader.IsDBNull(10) ? null : reader.GetString(10);
        return new MemberIdentity(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            reader.GetString(2), reader.GetString(3), NullableString(reader, 4), NullableString(reader, 5),
            NullableString(reader, 6), NullableString(reader, 7),
            DateOnly.ParseExact(reader.GetString(8), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ParseDecimal(reader.GetString(9)),
            photoFile is null ? null : $"/api/members/{reader.GetString(0)}/photo", reader.GetString(11));
    }

    private static TransactionInfo ReadTransaction(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetString(1), Guid.Parse(reader.GetString(2)),
            reader.GetString(3), reader.GetString(4), NullableString(reader, 5), ParseDecimal(reader.GetString(6)),
            DateOnly.ParseExact(reader.GetString(7), "yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetString(8),
            DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private async Task<string> CreateSessionAsync(SqliteConnection connection, SqliteTransaction? transaction, Guid memberId)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Sessions (TokenHash, MemberId, ExpiresAt) VALUES ($hash, $member, $expires);
            """;
        command.Parameters.AddWithValue("$hash", HashToken(token));
        command.Parameters.AddWithValue("$member", memberId.ToString());
        command.Parameters.AddWithValue("$expires", DateTime.UtcNow.AddDays(30).ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
        return token;
    }

    private async Task<AssociationInfo?> ReadAssociationByCodeAsync(
        SqliteConnection connection, SqliteTransaction transaction, string joinCode)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, Name, Purpose, MonthlyGoal, JoinCode FROM Associations WHERE JoinCode=$code COLLATE NOCASE;";
        command.Parameters.AddWithValue("$code", joinCode);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new AssociationInfo(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                ParseDecimal(reader.GetString(3)), reader.GetString(4))
            : null;
    }

    private static async Task<bool> RegisteredContactExistsAsync(
        SqliteConnection connection, SqliteTransaction transaction, string contact)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM Members WHERE Contact=$contact AND PasswordHash IS NOT NULL LIMIT 1;";
        command.Parameters.AddWithValue("$contact", contact);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<Guid?> FindPendingMemberAsync(
        SqliteConnection connection, SqliteTransaction transaction, Guid associationId, string contact)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id FROM Members
            WHERE AssociationId=$association AND Contact=$contact AND PasswordHash IS NULL LIMIT 1;
            """;
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$contact", contact);
        var id = await command.ExecuteScalarAsync() as string;
        return id is null ? null : Guid.Parse(id);
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON;";
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private async Task WriteEventAsync(Guid associationId, Guid memberId, string type, string summary)
    {
        await using var connection = await OpenAsync();
        await WriteEventAsync(connection, null, associationId, memberId, type, summary);
    }

    private static async Task WriteEventAsync(
        SqliteConnection connection, SqliteTransaction? transaction,
        Guid associationId, Guid memberId, string type, string summary)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO AuditEvents (Id, AssociationId, MemberId, Type, Summary, CreatedAt)
            VALUES ($id, $association, $member, $type, $summary, $created);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$association", associationId.ToString());
        command.Parameters.AddWithValue("$member", memberId.ToString());
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$summary", summary);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private static string NormalizeContact(string contact)
    {
        var value = contact.Trim().ToLowerInvariant();
        if (value.Contains('@', StringComparison.Ordinal))
            return value;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 9 && digits[0] is '2' or '3' or '6'
            ? "237" + digits
            : digits;
    }
    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NullableString(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
    private static string CreateJoinCode() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
}
