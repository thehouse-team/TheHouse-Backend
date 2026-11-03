using System.ComponentModel.DataAnnotations;

namespace TheHouse.Infrastructure.Configuration;

public sealed class PasswordHashingOptions
{
    public const int MinimumIterations = 210_000;
    public const int MaximumIterations = 2_000_000;

    [Range(MinimumIterations, MaximumIterations)]
    public int Iterations { get; set; } = MinimumIterations;
}
