namespace TheHouse.Http;

// Opt-in exclusivo para contenido público, sin secretos y sin reflexión de entrada sensible.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PublicResponseCompressionAttribute : Attribute;
