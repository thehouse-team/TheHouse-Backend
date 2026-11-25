using Azure.Core;
using TheHouse.Application.Common;
using TheHouse.Infrastructure.Moderation;

namespace TheHouse.Configuration;

// NV-03 1.3.0-u2: credencial de Azure.Core para el exportador de Application
// Insights sobre la identidad administrada de la app (la misma de Content
// Safety). Sin Azure.Identity, sin llaves ni secretos. La fuente ya guarda el
// token hasta 5 min antes de que venza; aquí se declara una vigencia corta
// para que Azure.Core vuelva a pedirlo y siempre reciba el de la fuente.
public sealed class ManagedIdentityTokenCredential(IManagedIdentityTokenSource source, TimeProvider timeProvider) : TokenCredential
{
    private static readonly TimeSpan Validity = TimeSpan.FromMinutes(10);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            var token = await source.GetTokenAsync(cancellationToken);
            return new AccessToken(token, timeProvider.GetUtcNow().Add(Validity));
        }
        catch (DependencyUnavailableException exception)
        {
            // Sin detalle: el mensaje nunca incluye el token ni la respuesta.
            throw new InvalidOperationException("Managed identity token unavailable for Azure Monitor.", exception);
        }
    }
}
