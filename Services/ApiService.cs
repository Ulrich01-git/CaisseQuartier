using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CaisseQuartier.Models;

namespace CaisseQuartier.Services;

public sealed class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class ApiService
{
    private const string ServerPreference = "caissequartier.server";
    private const string TokenKey = "caissequartier.token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static ApiService Shared { get; } = new();
    public MemberIdentity? CurrentMember { get; private set; }
    public AssociationInfo? CurrentAssociation { get; private set; }

    public string ServerUrl => Preferences.Default.Get(ServerPreference, DefaultServerUrl());

    public bool IsServerConfigured => Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) &&
                                      uri.Scheme == Uri.UriSchemeHttp;

    public void ConfigureServer(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535)
            throw new ApiException("Adresse invalide. Exemple : http://192.168.1.10:5080");

        Preferences.Default.Set(ServerPreference, uri.GetLeftPart(UriPartial.Authority).TrimEnd('/'));
    }

    public async Task<AuthResponse> LoginAsync(string contact, string password)
    {
        var response = await SendAsync<AuthResponse>(HttpMethod.Post, "/api/auth/login",
            new LoginRequest(contact, password), authenticated: false);
        await StoreAuthenticationAsync(response);
        return response;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await SendAsync<AuthResponse>(HttpMethod.Post, "/api/auth/register", request, authenticated: false);
        await StoreAuthenticationAsync(response);
        return response;
    }

    public async Task LogoutAsync()
    {
        try
        {
            await SendAsync<object>(HttpMethod.Post, "/api/auth/logout", null);
        }
        finally
        {
            SecureStorage.Default.Remove(TokenKey);
            CurrentMember = null;
            CurrentAssociation = null;
        }
    }

    public Task<MemberIdentity> GetMeAsync() =>
        SendAsync<MemberIdentity>(HttpMethod.Get, "/api/auth/me");

    public Task<AssociationInfo> GetAssociationAsync() =>
        SendAsync<AssociationInfo>(HttpMethod.Get, "/api/association");

    public Task<AssociationInfo> UpdateAssociationAsync(AssociationUpdateRequest request) =>
        SendAsync<AssociationInfo>(HttpMethod.Put, "/api/association", request);

    public Task<DashboardInfo> GetDashboardAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var query = new List<string>();
        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");
        return SendAsync<DashboardInfo>(HttpMethod.Get,
            "/api/dashboard" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query)));
    }

    public Task<List<MemberProfile>> GetMembersAsync(string? search = null)
    {
        var path = "/api/members";
        if (!string.IsNullOrWhiteSpace(search))
            path += "?search=" + Uri.EscapeDataString(search.Trim());
        return SendAsync<List<MemberProfile>>(HttpMethod.Get, path);
    }

    public Task<MemberProfile> GetMemberAsync(Guid id) =>
        SendAsync<MemberProfile>(HttpMethod.Get, $"/api/members/{id:D}");

    public Task<MemberProfile> AddMemberAsync(MemberCreateRequest request) =>
        SendAsync<MemberProfile>(HttpMethod.Post, "/api/members", request);

    public Task<MemberProfile> UpdateMemberAsync(Guid id, MemberUpdateRequest request) =>
        SendAsync<MemberProfile>(HttpMethod.Put, $"/api/members/{id:D}", request);

    public async Task<string> UploadMemberPhotoAsync(Guid id, byte[] bytes, string contentType)
    {
        using var request = await CreateRequestAsync(HttpMethod.Put, $"/api/members/{id:D}/photo");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await SendRequestAsync(request);
        var result = await response.Content.ReadFromJsonAsync<PhotoResponse>(JsonOptions);
        return result?.PhotoUrl ?? throw new ApiException("Le serveur n'a pas confirmé le chargement de la photo.");
    }

    public async Task<ImageSource?> GetMemberPhotoAsync(Guid id)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"/api/members/{id:D}/photo");
        using var response = await SendRequestAsync(request, allowNotFound: true);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    }

    public Task<List<TransactionInfo>> GetTransactionsAsync(
        DateOnly from, DateOnly to, string? type = null)
    {
        string path = $"/api/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(type)) path += $"&type={Uri.EscapeDataString(type)}";
        return SendAsync<List<TransactionInfo>>(HttpMethod.Get, path);
    }

    public Task<TransactionInfo> AddContributionAsync(TransactionCreateRequest request) =>
        SendAsync<TransactionInfo>(HttpMethod.Post, "/api/contributions", request);

    public Task<TransactionInfo> AddExpenseAsync(TransactionCreateRequest request) =>
        SendAsync<TransactionInfo>(HttpMethod.Post, "/api/expenses", request);

    public Task<List<AuditEventInfo>> GetEventsAsync(DateOnly from, DateOnly to) =>
        SendAsync<List<AuditEventInfo>>(HttpMethod.Get,
            $"/api/events?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public async Task<byte[]> DownloadReportAsync(
        DateOnly from, DateOnly to, string type, string period)
    {
        string path = $"/api/reports/pdf?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" +
                      $"&type={Uri.EscapeDataString(type)}&period={Uri.EscapeDataString(period)}";
        using var request = await CreateRequestAsync(HttpMethod.Get, path);
        using var response = await SendRequestAsync(request);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> DownloadProjectReportAsync()
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "/api/reports/project.pdf");
        using var response = await SendRequestAsync(request);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, bool authenticated = true)
    {
        using var request = await CreateRequestAsync(method, path, authenticated);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await SendRequestAsync(request);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return default!;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
               ?? throw new ApiException("Réponse vide reçue du serveur.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method, string path, bool authenticated = true)
    {
        if (!IsServerConfigured)
            throw new ApiException("Indiquez l'adresse Wi-Fi du PC qui héberge CaisseQuartier.");

        var request = new HttpRequestMessage(method, new Uri(new Uri(ServerUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        if (authenticated)
        {
            string? token = await SecureStorage.Default.GetAsync(TokenKey);
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiException("Votre session a expiré. Reconnectez-vous.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, bool allowNotFound = false)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiException($"Impossible de joindre le serveur ({ServerUrl}). Vérifiez que le PC et le téléphone sont sur le même Wi-Fi et que le serveur est démarré. Détail : {exception.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new ApiException($"Le serveur ne répond pas ({ServerUrl}). Vérifiez le réseau et réessayez.");
        }

        if (response.IsSuccessStatusCode || (allowNotFound && response.StatusCode == HttpStatusCode.NotFound))
            return response;

        ApiErrorResponse? error = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        string message = error?.Message ?? $"Erreur du serveur : {(int)response.StatusCode} {response.ReasonPhrase}.";
        var status = response.StatusCode;
        response.Dispose();
        throw new ApiException(message, status);
    }

    private async Task StoreAuthenticationAsync(AuthResponse response)
    {
        await SecureStorage.Default.SetAsync(TokenKey, response.Token);
        CurrentMember = response.Member;
        CurrentAssociation = response.Association;
    }

    private static string DefaultServerUrl()
    {
        if (DeviceInfo.Current.Platform == DevicePlatform.Android && DeviceInfo.Current.DeviceType == DeviceType.Virtual)
            return "http://10.0.2.2:5080";
        if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            return "http://localhost:5080";
        return string.Empty;
    }

    private sealed record PhotoResponse(string PhotoUrl);
}
