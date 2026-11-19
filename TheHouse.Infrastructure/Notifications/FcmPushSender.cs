using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Abstractions.Notifications;

namespace TheHouse.Infrastructure.Notifications;

// NV-20 1.2.0: cuenta de servicio de Firebase leída del secreto
// `FIREBASE_SERVICE_ACCOUNT` (JSON o JSON en base64). Nunca se registra.
public sealed class FirebaseServiceAccount
{
    public const string TokenUri = "https://oauth2.googleapis.com/token";

    private FirebaseServiceAccount(string projectId, string clientEmail, string privateKeyPem)
    {
        ProjectId = projectId;
        ClientEmail = clientEmail;
        PrivateKeyPem = privateKeyPem;
    }

    public string ProjectId { get; }

    public string ClientEmail { get; }

    public string PrivateKeyPem { get; }

    public static FirebaseServiceAccount? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var json = raw.TrimStart().StartsWith('{') ? raw : Decode(raw);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var project = Text(root, "project_id");
            var email = Text(root, "client_email");
            var key = Text(root, "private_key");
            var tokenUri = Text(root, "token_uri") ?? TokenUri;

            // Solo el proyecto y el endpoint de OAuth de Google (sin SSRF por configuración).
            if (project is null || email is null || key is null || tokenUri != TokenUri ||
                !project.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ||
                !key.Contains("PRIVATE KEY", StringComparison.Ordinal))
            {
                return null;
            }

            return new FirebaseServiceAccount(project, email, key);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Decode(string raw)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(raw.Trim()));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}

// Cliente de FCM (API HTTP v1) con OAuth 2.0 de cuenta de servicio: JWT
// RS256 firmado con la criptografía de .NET, sin paquetes de Firebase.
public sealed class FcmPushSender(HttpClient http, FirebaseServiceAccount account, TimeProvider timeProvider) : IPushSender, IDisposable
{
    public const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim gate = new(1, 1);

    private string? accessToken;

    private DateTimeOffset accessTokenExpiresAt;

    public bool Enabled => true;

    public async Task<PushResult> SendAsync(string fcmToken, PushMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await AccessTokenAsync(forceRenew: attempt > 0, cancellationToken);
            if (token is null)
            {
                return PushResult.Failed;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{account.ProjectId}/messages:send")
            {
                Content = JsonContent.Create(Payload(fcmToken, message)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return PushResult.Sent;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return IsInvalidToken(response.StatusCode, body) ? PushResult.InvalidToken : PushResult.Failed;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return PushResult.Failed;
            }
        }

        return PushResult.Failed;
    }

    // Formato del mensaje de FCM HTTP v1 (todos los valores de `data` son texto).
    public static object Payload(string fcmToken, PushMessage message) => new
    {
        message = new
        {
            token = fcmToken,
            notification = new { title = message.Title, body = message.Body },
            data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["notificationId"] = message.NotificationId,
                ["type"] = message.Type,
                ["category"] = message.Category,
                ["targetKind"] = message.TargetKind,
                ["targetId"] = message.TargetId,
            },
            android = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["priority"] = message.HighPriority ? "HIGH" : "NORMAL",
                ["notification"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["channel_id"] = message.Category },
            }.Concat(message.CollapseKey is null ? [] : [new KeyValuePair<string, object>("collapse_key", message.CollapseKey)])
             .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        },
    };

    public static bool IsInvalidToken(HttpStatusCode status, string body) =>
        status == HttpStatusCode.NotFound ||
        body.Contains("UNREGISTERED", StringComparison.Ordinal) ||
        (status == HttpStatusCode.BadRequest && body.Contains("registration token", StringComparison.OrdinalIgnoreCase));

    // JWT de la cuenta de servicio (RFC 7523) para pedir el token de acceso.
    public static string Assertion(FirebaseServiceAccount account, DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = FirebaseServiceAccount.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.Add(TokenLifetime).ToUnixTimeSeconds(),
        }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + claims), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + claims + "." + Base64Url(signature);
    }

    private async Task<string?> AccessTokenAsync(bool forceRenew, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!forceRenew && accessToken is not null && now < accessTokenExpiresAt - RenewBefore)
        {
            return accessToken;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (!forceRenew && accessToken is not null && now < accessTokenExpiresAt - RenewBefore)
            {
                return accessToken;
            }

            using var content = new FormUrlEncodedContent(
            [
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("assertion", Assertion(account, now)),
            ]);
            using var response = await http.PostAsync(FirebaseServiceAccount.TokenUri, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (!root.TryGetProperty("access_token", out var value) || value.GetString() is not { Length: > 0 } token)
            {
                return null;
            }

            var seconds = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var parsed) ? parsed : 3600;
            accessToken = token;
            accessTokenExpiresAt = now.AddSeconds(seconds);
            return token;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or CryptographicException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => gate.Dispose();
}

// Sin el secreto: los dispositivos se registran y la bandeja funciona, pero
// no se envía push.
public sealed class DisabledPushSender : IPushSender
{
    public bool Enabled => false;

    public Task<PushResult> SendAsync(string fcmToken, PushMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(PushResult.Disabled);
}
