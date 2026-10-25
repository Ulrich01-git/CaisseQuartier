using CaisseQuartier.Api;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5080");
builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<PdfReport>();

var app = builder.Build();
var store = app.Services.GetRequiredService<LocalStore>();
await store.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "CaisseQuartier" }));

app.MapPost("/api/auth/register", async (RegisterRequest request, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        return Results.BadRequest(new ApiError("Saisissez votre nom complet."));
    if (string.IsNullOrWhiteSpace(request.Contact) || request.Contact.Trim().Length < 5)
        return Results.BadRequest(new ApiError("Saisissez un téléphone ou un email valide."));
    if (request.Password is null || request.Password.Length < 8)
        return Results.BadRequest(new ApiError("Le mot de passe doit contenir au moins 8 caractères."));

    if (string.IsNullOrWhiteSpace(request.JoinCode) &&
        (string.IsNullOrWhiteSpace(request.AssociationName) || request.AssociationName.Trim().Length < 3 ||
         string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Trim().Length < 8 ||
     request.MonthlyGoal <= 0))
        return Results.BadRequest(new ApiError("Pour créer une association, indiquez son nom, son objectif et son objectif mensuel."));

    try
    {
        var result = await store.RegisterAsync(request);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(result);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new ApiError(exception.Message));
    }
});

app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.Contact) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new ApiError("Saisissez vos identifiants."));

    var result = await store.LoginAsync(request.Contact.Trim(), request.Password);
    if (result is null)
        return Results.Json(new ApiError("Identifiant ou mot de passe incorrect."), statusCode: StatusCodes.Status401Unauthorized);

    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(result);
});

app.MapPost("/api/auth/logout", async (HttpContext context) =>
{
    var token = ReadBearerToken(context);
    if (token is not null)
        await store.RevokeSessionAsync(token);
    return Results.NoContent();
});

app.MapGet("/api/auth/me", async (HttpContext context) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    return member is null ? Results.Unauthorized() : Results.Ok(member);
});

app.MapGet("/api/association", async (HttpContext context) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    return Results.Ok(await store.GetAssociationAsync(member.AssociationId));
});

app.MapPut("/api/association", async (AssociationUpdateRequest request, HttpContext context) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    if (member.Role != "admin") return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 3 ||
        string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Trim().Length < 8 ||
        request.MonthlyGoal <= 0)
        return Results.BadRequest(new ApiError("Vérifiez le nom, l'objectif de l'association et le montant mensuel."));

    await store.UpdateAssociationAsync(member.AssociationId, request);
    await store.LogEventAsync(member.AssociationId, member.Id, "association_updated",
        $"Projet associatif mis à jour : {request.Name.Trim()}");
    return Results.Ok(await store.GetAssociationAsync(member.AssociationId));
});

app.MapGet("/api/dashboard", async (HttpContext context, DateOnly? from, DateOnly? to) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    var start = from ?? new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
    var end = to ?? DateOnly.FromDateTime(DateTime.Today);
    if (start > end) return Results.BadRequest(new ApiError("La période est invalide."));
    return Results.Ok(await store.GetDashboardAsync(member.AssociationId, start, end));
});

app.MapGet("/api/members", async (HttpContext context, string? search) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    var members = await store.GetMembersAsync(member.AssociationId, search);
    return Results.Ok(members.Select(profile =>
        profile with { CanEdit = profile.Id == member.Id || member.Role == "admin" }).ToList());
});

app.MapGet("/api/members/{id:guid}", async (Guid id, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    var member = await store.GetMemberAsync(current.AssociationId, id);
    if (member is null) return Results.NotFound(new ApiError("Membre introuvable."));
    return Results.Ok(member with { CanEdit = member.Id == current.Id || current.Role == "admin" });
});

app.MapGet("/api/members/{id:guid}/photo", async (Guid id, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    var photo = await store.ReadMemberPhotoAsync(current.AssociationId, id);
    return photo is null
        ? Results.NotFound(new ApiError("Aucune photo de profil disponible."))
        : Results.File(photo.Value.Bytes, photo.Value.ContentType);
});

app.MapPost("/api/members", async (MemberCreateRequest request, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        return Results.BadRequest(new ApiError("Le nom du membre est obligatoire."));
    if (request.MonthlyContribution < 0 || string.IsNullOrWhiteSpace(request.Contact))
        return Results.BadRequest(new ApiError("Saisissez un contact et une cotisation mensuelle positive ou nulle."));

    try
    {
        var member = await store.AddMemberAsync(current.AssociationId, request);
        await store.LogEventAsync(current.AssociationId, current.Id, "member_added",
            $"Nouveau profil membre : {request.FullName.Trim()}");
        return Results.Created($"/api/members/{member.Id}", member);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new ApiError(exception.Message));
    }
});

app.MapPut("/api/members/{id:guid}", async (Guid id, MemberUpdateRequest request, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    if (id != current.Id && current.Role != "admin") return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2 ||
        request.MonthlyContribution < 0)
        return Results.BadRequest(new ApiError("Vérifiez le nom et le montant de la cotisation."));
    var member = await store.UpdateMemberAsync(current.AssociationId, id, current.Id, request);
    return member is null
        ? Results.NotFound(new ApiError("Membre introuvable."))
        : Results.Ok(member with { CanEdit = true });
});

app.MapPut("/api/members/{id:guid}/photo", async (Guid id, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    if (id != current.Id && current.Role != "admin") return Results.Forbid();
    context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize = 5_000_000;
    if (context.Request.ContentLength is > 5_000_000)
        return Results.BadRequest(new ApiError("La photo ne doit pas dépasser 5 Mo."));

    var contentType = context.Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
    var extension = contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        _ => null
    };
    if (extension is null)
        return Results.BadRequest(new ApiError("Formats acceptés : JPEG ou PNG."));

    await using var buffer = new MemoryStream();
    await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
    if (buffer.Length is 0 or > 5_000_000)
        return Results.BadRequest(new ApiError("La photo est vide ou dépasse 5 Mo."));
    var bytes = buffer.ToArray();
    bool validSignature = extension == ".jpg"
        ? bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff
        : bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    if (!validSignature)
        return Results.BadRequest(new ApiError("Le fichier reçu n'est pas une image valide."));

    var photoUrl = await store.SaveMemberPhotoAsync(current.AssociationId, id, current.Id, bytes, extension);
    return photoUrl is null
        ? Results.NotFound(new ApiError("Membre introuvable."))
        : Results.Ok(new { photoUrl });
});

app.MapGet("/api/transactions", async (HttpContext context, DateOnly? from, DateOnly? to, string? type) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    if (!IsValidTransactionType(type)) return Results.BadRequest(new ApiError("Type inconnu."));
    var start = from ?? DateOnly.MinValue;
    var end = to ?? DateOnly.MaxValue;
    if (start > end) return Results.BadRequest(new ApiError("La période est invalide."));
    return Results.Ok(await store.GetTransactionsAsync(member.AssociationId, start, end, type));
});

app.MapGet("/api/events", async (HttpContext context, DateOnly? from, DateOnly? to) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    var start = from ?? DateOnly.MinValue;
    var end = to ?? DateOnly.MaxValue;
    if (start > end) return Results.BadRequest(new ApiError("La période est invalide."));
    return Results.Ok(await store.GetEventsAsync(member.AssociationId, start, end));
});

app.MapPost("/api/contributions", async (TransactionCreateRequest request, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount ||
        request.MemberId == Guid.Empty)
        return Results.BadRequest(new ApiError("Choisissez un membre et saisissez un montant entier positif en FCFA."));
    var transaction = await store.AddTransactionAsync(current.AssociationId, current.Id, "contribution", request);
    return transaction is null
        ? Results.BadRequest(new ApiError("Le membre sélectionné n'appartient pas à cette association."))
        : Results.Created($"/api/transactions/{transaction.Id}", transaction);
});

app.MapPost("/api/expenses", async (TransactionCreateRequest request, HttpContext context) =>
{
    var current = await GetCurrentMemberAsync(context, store);
    if (current is null) return Results.Unauthorized();
    if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount ||
        string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 2)
        return Results.BadRequest(new ApiError("Saisissez un libellé et un montant entier positif en FCFA."));
    var transaction = await store.AddTransactionAsync(current.AssociationId, current.Id, "expense", request);
    return Results.Created($"/api/transactions/{transaction!.Id}", transaction);
});

app.MapGet("/api/reports/pdf", async (HttpContext context, DateOnly from, DateOnly to, string? type, string? period) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();
    if (from > to) return Results.BadRequest(new ApiError("La période est invalide."));
    if (!IsValidReportType(type)) return Results.BadRequest(new ApiError("Type de rapport inconnu."));

    var association = await store.GetAssociationAsync(member.AssociationId);
    var rows = type == "event"
        ? (await store.GetEventsAsync(member.AssociationId, from, to))
            .Select(item => new TransactionInfo(item.Id, "event", member.Id, item.ActorName,
                item.Summary, item.Type, 0, DateOnly.FromDateTime(item.CreatedAt), item.ActorName, item.CreatedAt))
            .ToList()
        : await store.GetTransactionsAsync(member.AssociationId, from, to, type);
    var bytes = app.Services.GetRequiredService<PdfReport>().Create(association, rows, from, to, type, period);
    var filename = $"CaisseQuartier-{type ?? "activite"}-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf", filename);
});

app.MapGet("/api/reports/project.pdf", async (HttpContext context) =>
{
    var member = await GetCurrentMemberAsync(context, store);
    if (member is null) return Results.Unauthorized();

    var association = await store.GetAssociationAsync(member.AssociationId);
    if (association is null) return Results.NotFound(new ApiError("Association introuvable."));

    var bytes = app.Services.GetRequiredService<PdfReport>()
        .CreateProjectReport(association, DateTime.Now);
    return Results.File(bytes, "application/pdf", "CaisseQuartier-Rapport-du-projet.pdf");
});

app.MapFallback(() => Results.NotFound(new ApiError("Route API inconnue.")));
app.Run();

static bool IsValidTransactionType(string? type) =>
    string.IsNullOrWhiteSpace(type) || type is "contribution" or "expense";

static bool IsValidReportType(string? type) =>
    string.IsNullOrWhiteSpace(type) || type is "contribution" or "expense" or "event";

static string? ReadBearerToken(HttpContext context)
{
    var authorization = context.Request.Headers.Authorization.ToString();
    return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authorization["Bearer ".Length..].Trim()
        : null;
}

static Task<MemberIdentity?> GetCurrentMemberAsync(HttpContext context, LocalStore store)
{
    var token = ReadBearerToken(context);
    return token is null ? Task.FromResult<MemberIdentity?>(null) : store.GetSessionAsync(token);
}
