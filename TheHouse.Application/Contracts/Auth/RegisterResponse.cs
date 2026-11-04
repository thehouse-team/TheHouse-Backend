namespace TheHouse.Application.Contracts.Auth;

public sealed record RegisterResponse(string Id, string Email, DateTimeOffset CreatedAt);
