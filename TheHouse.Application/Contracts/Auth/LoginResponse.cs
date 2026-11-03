namespace TheHouse.Application.Contracts.Auth;

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);
