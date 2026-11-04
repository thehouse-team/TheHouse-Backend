using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    [Required]
    [StringLength(254, MinimumLength = 1)]
    public string? Email { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string? Password { get; init; }
}
