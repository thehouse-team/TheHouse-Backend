using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Notifications;
using TheHouse.TestDoubles;

namespace TheHouse.IntegrationTests;

// NV-20 1.2.0: cliente de FCM contra un servidor falso, con una llave RSA
// generada al momento (ninguna credencial real en el repositorio).
public sealed class FcmClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider clock = new(Now);

    [Fact]
    public void Service_account_is_parsed_from_json_or_base64_and_only_for_google()
    {
        var json = ServiceAccountJson(out _);
        Assert.Equal("thehouse-test", FirebaseServiceAccount.Parse(json)!.ProjectId);
        Assert.NotNull(FirebaseServiceAccount.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes(json))));
        Assert.Null(FirebaseServiceAccount.Parse(json.Replace(FirebaseServiceAccount.TokenUri, "https://evil.example/token", StringComparison.Ordinal)));
        Assert.Null(FirebaseServiceAccount.Parse("no-json"));
        Assert.Null(FirebaseServiceAccount.Parse(null));
    }

    [Fact]
    public async Task Fcm_client_signs_the_assertion_caches_the_token_and_maps_errors()
    {
        var account = FirebaseServiceAccount.Parse(ServiceAccountJson(out var rsa))!;
        using var key = rsa;
        var handler = new FakeFcm();
        using var http = new HttpClient(handler);
        using var sender = new FcmPushSender(http, account, clock);

        Assert.Equal(PushResult.Sent, await sender.SendAsync(Token("ok"), Message(), CancellationToken.None));
        Assert.Equal(PushResult.Sent, await sender.SendAsync(Token("ok"), Message(), CancellationToken.None));
        Assert.Equal(1, handler.TokenRequests);

        // El JWT va firmado con la llave de la cuenta y pide solo el alcance de FCM.
        var parts = handler.LastAssertion!.Split('.');
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), FromBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using (var claims = JsonDocument.Parse(FromBase64Url(parts[1])))
        {
            Assert.Equal(FcmPushSender.Scope, claims.RootElement.GetProperty("scope").GetString());
            Assert.Equal(FirebaseServiceAccount.TokenUri, claims.RootElement.GetProperty("aud").GetString());
        }

        // Formato HTTP v1: token, notificación, datos de texto y Android.
        using (var body = JsonDocument.Parse(handler.LastMessage!))
        {
            var message = body.RootElement.GetProperty("message");
            Assert.Equal(Token("ok"), message.GetProperty("token").GetString());
            Assert.Equal("n1", message.GetProperty("data").GetProperty("notificationId").GetString());
            Assert.Equal("HIGH", message.GetProperty("android").GetProperty("priority").GetString());
            Assert.Equal("requests", message.GetProperty("android").GetProperty("notification").GetProperty("channel_id").GetString());
            Assert.Equal("conversation-c1", message.GetProperty("android").GetProperty("collapse_key").GetString());
        }

        handler.Next = HttpStatusCode.NotFound;
        Assert.Equal(PushResult.InvalidToken, await sender.SendAsync(Token("gone"), Message(), CancellationToken.None));
        handler.Next = HttpStatusCode.InternalServerError;
        Assert.Equal(PushResult.Failed, await sender.SendAsync(Token("ok"), Message(), CancellationToken.None));

        // 401: renueva el token de acceso y reintenta una vez.
        handler.Next = HttpStatusCode.Unauthorized;
        Assert.Equal(PushResult.Sent, await sender.SendAsync(Token("ok"), Message(), CancellationToken.None));
        Assert.Equal(2, handler.TokenRequests);

        // Antes de vencer (5 min de margen) se pide un token nuevo.
        clock.Now = Now.AddMinutes(56);
        await sender.SendAsync(Token("ok"), Message(), CancellationToken.None);
        Assert.Equal(3, handler.TokenRequests);
    }

    private static string Token(string seed) => "fcm-" + seed.PadRight(24, 'x') + ":APA91b";

    private static PushMessage Message() =>
        new("Mensaje nuevo", "Ana te escribió sobre Casa Verde.", "n1", NotificationTypes.MessageReceived, "requests", "conversation", "c1", true, "conversation-c1");

    // Cuenta de servicio de prueba con una llave RSA generada al momento.
    private static string ServiceAccountJson(out RSA rsa)
    {
        rsa = RSA.Create(2048);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "thehouse-test",
            ["client_email"] = "push@thehouse-test.iam.gserviceaccount.com",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = FirebaseServiceAccount.TokenUri,
        });
    }

    private static byte[] FromBase64Url(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
    }

    private sealed class FakeFcm : HttpMessageHandler
    {
        public int TokenRequests;

        public string? LastAssertion;

        public string? LastMessage;

        public HttpStatusCode Next = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString() == FirebaseServiceAccount.TokenUri)
            {
                TokenRequests++;
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastAssertion = Uri.UnescapeDataString(form.Split("assertion=")[1]);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"test-access-" + TokenRequests + "\",\"expires_in\":3600}", Encoding.UTF8, "application/json"),
                };
            }

            Assert.Equal("https://fcm.googleapis.com/v1/projects/thehouse-test/messages:send", request.RequestUri.ToString());
            Assert.StartsWith("test-access-", request.Headers.Authorization!.Parameter, StringComparison.Ordinal);
            LastMessage = await request.Content!.ReadAsStringAsync(cancellationToken);
            var status = Next;
            Next = HttpStatusCode.OK;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.NotFound ? "{\"error\":{\"status\":\"NOT_FOUND\",\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}" : "{}"),
            };
        }
    }
}
