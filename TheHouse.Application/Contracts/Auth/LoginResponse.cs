namespace TheHouse.Application.Contracts.Auth;

// NV-10: `RefreshToken` se agrega al final de forma aditiva; los tres
// primeros campos conservan nombre, tipo y significado de NV-05.
public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken);
