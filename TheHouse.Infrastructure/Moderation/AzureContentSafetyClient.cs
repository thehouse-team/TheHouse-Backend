using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Moderation;

// NV-21 1.2.0: token de la identidad administrada de la Container App
// (variables IDENTITY_ENDPOINT e IDENTITY_HEADER que pone Azure). Sin llave.
public interface IManagedIdentityTokenSource
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

// `clientId` elige la identidad asignada por el usuario (Terraform `id-thehouse-u2-app`).
// `resource` permite pedir tokens para otro servicio con la misma identidad
// (NV-03 1.3.0-u2: Application Insights usa https://monitor.azure.com).
public sealed class ContainerAppsIdentityTokenSource(HttpClient http, string? identityEndpoint, string? identityHeader, string? clientId, TimeProvider timeProvider, string resource = ContainerAppsIdentityTokenSource.Resource)
    : IManagedIdentityTokenSource, IDisposable
{
    public const string Resource = "https://cognitiveservices.azure.com";

    public const string MonitorResource = "https://monitor.azure.com";

    private readonly SemaphoreSlim gate = new(1, 1);

    private string? token;

    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (token is not null && timeProvider.GetUtcNow() < expiresAt.AddMinutes(-5))
        {
            return token;
        }

        if (string.IsNullOrEmpty(identityEndpoint) || string.IsNullOrEmpty(identityHeader))
        {
            throw new DependencyUnavailableException();
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (token is not null && timeProvider.GetUtcNow() < expiresAt.AddMinutes(-5))
            {
                return token;
            }

            var client = string.IsNullOrEmpty(clientId) ? string.Empty : "&client_id=" + Uri.EscapeDataString(clientId);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{identityEndpoint}?api-version=2019-08-01&resource={Uri.EscapeDataString(resource)}{client}");
            request.Headers.Add("X-IDENTITY-HEADER", identityHeader);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new DependencyUnavailableException();
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            token = root.GetProperty("access_token").GetString() ?? throw new DependencyUnavailableException();
            expiresAt = root.TryGetProperty("expires_on", out var expires) && long.TryParse(
                expires.ValueKind == JsonValueKind.String ? expires.GetString() : expires.GetRawText(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : timeProvider.GetUtcNow().AddMinutes(30);
            return token;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException();
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}

// Cliente de Azure AI Content Safety (REST, versión GA 2023-10-01).
// Tiempo máximo de 3 s y un reintento; si no responde o se agota el cupo
// (429), lanza DependencyUnavailableException y el caso de uso falla cerrado.
//
// `failure` recibe el motivo de cada falla (sin contenido ni tokens) para que
// la API lo registre: token, timeout, quota, status-NNN o invalid-response.
public sealed class AzureContentSafetyClient(HttpClient http, Uri endpoint, IManagedIdentityTokenSource tokens, Action<string>? failure = null) : IContentSafetyClient
{
    public const string ApiVersion = "2023-10-01";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    // El token de la identidad tarda más en frío: tiene su propio límite.
    public static readonly TimeSpan TokenTimeout = TimeSpan.FromSeconds(10);

    public bool Enabled => true;

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeTextAsync(string text, CancellationToken cancellationToken) =>
        AnalyzeAsync("text:analyze", new { text, outputType = "FourSeverityLevels" }, cancellationToken);

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        AnalyzeAsync("image:analyze", new { image = new { content = Convert.ToBase64String(image.Span) }, outputType = "FourSeverityLevels" }, cancellationToken);

    private async Task<IReadOnlyList<ContentSafetyScore>> AnalyzeAsync(string operation, object body, CancellationToken cancellationToken)
    {
        var url = new Uri(endpoint, $"contentsafety/{operation}?api-version={ApiVersion}");
        var token = await TokenAsync(cancellationToken);
        for (var attempt = 0; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new("Bearer", token);
                using var response = await http.SendAsync(request, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                }

                // 429 (cupo F0 agotado) o 4xx de configuración: no tiene caso reintentar.
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode < 500 || attempt > 0)
                {
                    throw Fail(response.StatusCode == HttpStatusCode.TooManyRequests ? "quota" : $"status-{(int)response.StatusCode}");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt > 0)
                {
                    throw Fail(exception is TaskCanceledException ? "timeout" : exception is HttpRequestException ? "network" : "invalid-response");
                }
            }
        }
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TokenTimeout);
        try
        {
            return await tokens.GetTokenAsync(timeout.Token);
        }
        catch (Exception exception) when (exception is DependencyUnavailableException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw Fail("token");
        }
    }

    private DependencyUnavailableException Fail(string reason)
    {
        failure?.Invoke(reason);
        return new DependencyUnavailableException();
    }

    public static IReadOnlyList<ContentSafetyScore> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.GetProperty("categoriesAnalysis").EnumerateArray()
            .Select(item => new ContentSafetyScore(
                item.GetProperty("category").GetString() ?? string.Empty,
                item.TryGetProperty("severity", out var severity) && severity.ValueKind == JsonValueKind.Number ? severity.GetInt32() : 0))];
    }
}

// Sin el recurso configurado: no llama a la nube (solo la lista de términos).
public sealed class DisabledContentSafetyClient : IContentSafetyClient
{
    public bool Enabled => false;

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ContentSafetyScore>>([]);

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ContentSafetyScore>>([]);
}
