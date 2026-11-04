using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterRequest
{
    [Required]
    // El máximo de email se valida después de trim/lowercase, no sobre la entrada cruda.
    public string? Email { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string? Password { get; init; }
}
