// Casos de prueba de las reglas en thehouse.yml (`semgrep --test .semgrep/`).
// No forma parte de la solución: no se compila ni se despliega.
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;

namespace TheHouse.SemgrepRuleTests;

public class SemgrepRuleCases
{
    public void Logging(ILogger logger, LoginRequest request, string token, CancellationToken cancellationToken)
    {
        // ruleid: thehouse-log-sensitive-data
        logger.LogInformation("Inicio de sesión {Password}", request.Password);
        // ruleid: thehouse-log-sensitive-data
        logger.LogWarning("Token recibido {Value}", token);
        // ok: thehouse-log-sensitive-data
        logger.LogInformation("Inicio de sesión para {Email}", request.Email);
        // ok: thehouse-log-sensitive-data
        logger.LogDebug("Solicitud cancelada {State}", cancellationToken);
    }

    public void Connection()
    {
        // ruleid: thehouse-hardcoded-mongodb-credentials
        var uri = "mongodb+srv://usuario:clave@cluster.example.net/db";
        // ok: thehouse-hardcoded-mongodb-credentials
        var safe = "mongodb+srv://cluster.example.net/db";
    }

    public void Keys(JwtOptions options)
    {
        // ruleid: thehouse-hardcoded-signing-key
        options.SigningKey = "clave-de-firma-incrustada-en-codigo";
        // ok: thehouse-hardcoded-signing-key
        options.SigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    }

    public void Tls()
    {
        var handler = new HttpClientHandler();
        // ruleid: thehouse-tls-validation-disabled
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        // ruleid: thehouse-tls-validation-disabled
        handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => true;
    }

    public void Hashes(byte[] data)
    {
        // ruleid: thehouse-weak-hash
        var md5 = MD5.HashData(data);
        // ruleid: thehouse-weak-hash
        var sha1 = SHA1.Create();
        // ok: thehouse-weak-hash
        var sha256 = SHA256.HashData(data);
    }

    public void Randoms()
    {
        // ruleid: thehouse-insecure-random
        var weak = new Random();
        // ok: thehouse-insecure-random
        var strong = RandomNumberGenerator.GetBytes(16);
    }

    public void Jwt()
    {
        var parameters = new TokenValidationParameters
        {
            // ruleid: thehouse-jwt-validation-disabled
            ValidateLifetime = false,
            // ok: thehouse-jwt-validation-disabled
            ValidateIssuer = true
        };
    }

    public IResult Errors(Exception exception)
    {
        // ruleid: thehouse-exception-details-to-client
        return Results.Problem(detail: exception.Message);
    }

    public IResult SafeErrors()
    {
        // ok: thehouse-exception-details-to-client
        return Results.Problem(detail: "No se pudo completar la solicitud.");
    }

    public void Mongo(string userInput)
    {
        // ruleid: thehouse-bson-parse-input
        var filter = BsonDocument.Parse(userInput);
        // ok: thehouse-bson-parse-input
        var fixedFilter = BsonDocument.Parse("{ \"code\": 1 }");
    }
}
