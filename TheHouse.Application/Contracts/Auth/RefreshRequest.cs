using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Auth;

// NV-10: entrada de POST /auth/refresh. Campos desconocidos → 400.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RefreshRequest
{
    [Required]
    public string? RefreshToken { get; init; }
}
