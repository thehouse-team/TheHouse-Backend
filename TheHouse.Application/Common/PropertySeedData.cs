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

    // NV-07 1.2.0-u2: doce propiedades de demostración en la región
    // Tula–Tepeji–Jilotepec, vinculadas por código al catálogo de NV-08.
    // Sin direcciones exactas, coordenadas, contactos ni datos personales.
    public static IReadOnlyCollection<PropertySeedRecord> Create() =>
    [
        new PropertySeedRecord
        {
            Id = "000000000000000000000001",
            Title = "Cuarto amueblado cerca de la UTTT",
            Description = "Cuarto individual amueblado a distancia caminable de la universidad, con internet incluido.",
            Price = 2200.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UTTT",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 16,
            Amenities = ["wifi", "amueblado"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000002",
            Title = "Departamento de 2 recámaras en El Carmen",
            Description = "Departamento para compartir entre dos estudiantes, con cocina y estacionamiento.",
            Price = 6500.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UTTT",
            Bedrooms = 2,
            Bathrooms = 1,
            AreaM2 = 62,
            Amenities = ["wifi", "estacionamiento", "cocina equipada"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000003",
            Title = "Cuarto con baño propio en Tula centro",
            Description = "Cuarto con baño privado y servicios incluidos, cerca del transporte público.",
            Price = 2800.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UCAN",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 20,
            Amenities = ["wifi", "baño privado", "servicios incluidos"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000004",
            Title = "Estudio para estudiante en Tula",
            Description = "Estudio compacto con cocineta, ideal para una persona.",
            Price = 3500.0m,
            Currency = "MXN",
            City = "Tula de Allende",
            UniversityCode = "UNE-TULA",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 28,
            Amenities = ["wifi", "amueblado", "cocina equipada"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000005",
            Title = "Cuarto en casa compartida en Tepeji",
            Description = "Cuarto en casa compartida con áreas comunes y lavadora.",
            Price = 1900.0m,
            Currency = "MXN",
            City = "Tepeji del Río de Ocampo",
            UniversityCode = "UAEH-ESTE",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 14,
            Amenities = ["wifi", "lavadora"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000006",
            Title = "Departamento pequeño en Tepeji centro",
            Description = "Departamento de una recámara con cocina y servicios básicos.",
            Price = 5200.0m,
            Currency = "MXN",
            City = "Tepeji del Río de Ocampo",
            UniversityCode = "UAEH-ESTE",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 40,
            Amenities = ["wifi", "cocina equipada"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000007",
            Title = "Cuarto cerca de la Escuela Superior de Tlahuelilpan",
            Description = "Cuarto sencillo con internet, a pocos minutos de la escuela.",
            Price = 1800.0m,
            Currency = "MXN",
            City = "Tlahuelilpan",
            UniversityCode = "UAEH-ESTL",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 14,
            Amenities = ["wifi"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000008",
            Title = "Cuarto amueblado en Atotonilco de Tula",
            Description = "Cuarto amueblado con servicios incluidos y acceso a cocina compartida.",
            Price = 2000.0m,
            Currency = "MXN",
            City = "Atotonilco de Tula",
            UniversityCode = "UAEH-ESAT",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 15,
            Amenities = ["wifi", "amueblado", "servicios incluidos"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000009",
            Title = "Casa compartida en Mixquiahuala",
            Description = "Cuarto en casa compartida con patio y estacionamiento.",
            Price = 2300.0m,
            Currency = "MXN",
            City = "Mixquiahuala de Juárez",
            UniversityCode = "ITSOEH",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 16,
            Amenities = ["wifi", "estacionamiento"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000010",
            Title = "Cuarto cerca del TESJI",
            Description = "Cuarto individual con internet y escritorio para estudiar.",
            Price = 2100.0m,
            Currency = "MXN",
            City = "Jilotepec",
            UniversityCode = "TESJI",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 15,
            Amenities = ["wifi", "amueblado"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000011",
            Title = "Departamento de 1 recámara en Jilotepec",
            Description = "Departamento independiente con cocina, cerca de la unidad de la UMB.",
            Price = 4800.0m,
            Currency = "MXN",
            City = "Jilotepec",
            UniversityCode = "UMB-JILO",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 38,
            Amenities = ["wifi", "cocina equipada", "estacionamiento"],
            CreatedAt = SeedCreatedAt
        },
        new PropertySeedRecord
        {
            Id = "000000000000000000000012",
            Title = "Cuarto con servicios incluidos en Villa del Carbón",
            Description = "Cuarto con agua, luz e internet incluidos en la renta.",
            Price = 1900.0m,
            Currency = "MXN",
            City = "Villa del Carbón",
            UniversityCode = "UMB-VDC",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaM2 = 14,
            Amenities = ["wifi", "servicios incluidos"],
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
