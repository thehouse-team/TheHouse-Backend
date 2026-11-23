namespace TheHouse.Http;

// NV-06: convención reutilizable para endpoints autenticados.
public static class RevocationGuardExtensions
{
    public static TBuilder RequireTokenNotRevoked<TBuilder>(
        this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(RevocationGuard.Instance);

    // NV-23: endpoint público que acepta una sesión opcional.
    public static TBuilder AllowOptionalToken<TBuilder>(
        this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(RevocationGuard.OptionalInstance);
}
