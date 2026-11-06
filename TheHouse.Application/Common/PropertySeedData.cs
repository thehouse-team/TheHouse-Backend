namespace TheHouse.Application.Common;

public static class PropertySeedData
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

    public static IReadOnlyCollection<PropertySeedRecord> Create() =>
    [
        new PropertySeedRecord
        {
            Id = "000000000000000000000001",
            Title = "Departamento cerca de UTTT",
            Description = "Departamento de dos recámaras con estacionamiento y servicios incluidos.",
            Price = 8500.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UTTT",
            Bedrooms = 2,
            Bathrooms = 1,
            AreaM2 = 68,
            Amenities = ["wifi", "estacionamiento", "amueblado"],
            CreatedAt = SeedCreatedAt
        },

        new PropertySeedRecord
        {
            Id = "000000000000000000000002",
            Title = "Cuarto para estudiante UTTT",
            Description = "Cuarto individual amueblado a distancia caminable de la universidad.",
            Price = 4200.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UTTT",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 24,
            Amenities = ["wifi", "amueblado"],
            CreatedAt = SeedCreatedAt
        },

        new PropertySeedRecord
        {
            Id = "000000000000000000000003",
            Title = "Departamento en Pachuca centro",
            Description = "Departamento amplio con cocina equipada y estacionamiento.",
            Price = 14500.0m,
            Currency = "MXN",
            City = "Pachuca de Soto",
            UniversityCode = "UAEH",
            Bedrooms = 3,
            Bathrooms = 2,
            AreaM2 = 95,
            Amenities = ["wifi", "cocina equipada", "estacionamiento"],
            CreatedAt = SeedCreatedAt
        },

        new PropertySeedRecord
        {
            Id = "000000000000000000000004",
            Title = "Estudio cerca de UAEH",
            Description = "Estudio compacto ideal para una persona, con servicios incluidos.",
            Price = 6200.0m,
            Currency = "MXN",
            City = "Pachuca de Soto",
            UniversityCode = "UAEH",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 32,
            Amenities = ["wifi", "amueblado"],
            CreatedAt = SeedCreatedAt
        },

        new PropertySeedRecord
        {
            Id = "000000000000000000000005",
            Title = "Casa compartida en Mixquiahuala",
            Description = "Casa compartida con áreas comunes amplias y estacionamiento.",
            Price = 9800.0m,
            Currency = "MXN",
            City = "Mixquiahuala de Juárez",
            UniversityCode = "ITSOEH",
            Bedrooms = 3,
            Bathrooms = 2,
            AreaM2 = 110,
            Amenities = ["amueblado", "estacionamiento", "cocina equipada"],
            CreatedAt = SeedCreatedAt
        },

        new PropertySeedRecord
        {
            Id = "000000000000000000000006",
            Title = "Cuarto económico cerca de ITSOEH",
            Description = "Cuarto sencillo con acceso a internet y servicios básicos.",
            Price = 3500.0m,
            Currency = "MXN",
            City = "Mixquiahuala de Juárez",
            UniversityCode = "ITSOEH",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 20,
            Amenities = ["wifi"],
            CreatedAt = SeedCreatedAt
        }
    ];
}

public sealed class PropertySeedRecord
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; } = "MXN";

    public string City { get; init; } = string.Empty;

    public string UniversityCode { get; init; } = string.Empty;

    public int Bedrooms { get; init; }

    public int Bathrooms { get; init; }

    public double AreaM2 { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } =
        Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; init; }
}
