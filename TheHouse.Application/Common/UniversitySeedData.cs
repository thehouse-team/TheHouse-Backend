using TheHouse.Domain.Entities;

namespace TheHouse.Application.Common;

public static class UniversitySeedData
{
    public static readonly DateTimeOffset SeedCreatedAt =
        new(
            2026,
            11,
            2,
            0,
            0,
            0,
            TimeSpan.Zero);

    public static IReadOnlyCollection<University> Create() =>
    [
        new University
        {
            Code = "UTTT",
            Name = "Universidad Tecnológica de Tula-Tepeji",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },

        new University
        {
            Code = "UAEH",
            Name = "Universidad Autónoma del Estado de Hidalgo",
            City = "Pachuca de Soto",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },

        new University
        {
            Code = "ITSOEH",
            Name = "Instituto Tecnológico Superior del Occidente del Estado de Hidalgo",
            City = "Mixquiahuala de Juárez",
            Country = "México",
            CreatedAt = SeedCreatedAt
        }
    ];
}
