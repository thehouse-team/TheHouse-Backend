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

    // NV-08 1.2.0-u2: catálogo regional Tula–Tepeji–Jilotepec (Hidalgo y
    // Estado de México). Solo nombres institucionales públicos; fuentes en
    // specs/nv-08-university-catalog/spec.md §7.1.
    // NV-11: coordenadas de OpenStreetMap (© colaboradores de OpenStreetMap,
    // ODbL); ids y precisión en specs/nv-11-profile-preferences/evidence.md.
    public static IReadOnlyCollection<University> Create() =>
    [
        new University
        {
            Code = "UTTT",
            Name = "Universidad Tecnológica de Tula-Tepeji",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM way 386380501: campus.
            Latitude = 20.00903,
            Longitude = -99.34471
        },
        new University
        {
            Code = "UAEH-ESTE",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tepeji del Río",
            City = "Tepeji del Río de Ocampo",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM way 571163606: campus.
            Latitude = 19.91703,
            Longitude = -99.36116
        },
        new University
        {
            Code = "UAEH-ESTL",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tlahuelilpan",
            City = "Tlahuelilpan",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM node 459392250: centro de la localidad (aproximada).
            Latitude = 20.12991,
            Longitude = -99.23041
        },
        new University
        {
            Code = "UAEH-ESAT",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula",
            City = "Atotonilco de Tula",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM way 689177952: campus.
            Latitude = 20.01584,
            Longitude = -99.23601
        },
        new University
        {
            Code = "ITSOEH",
            Name = "Instituto Tecnológico Superior del Occidente del Estado de Hidalgo",
            City = "Mixquiahuala de Juárez",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM way 403660593: campus.
            Latitude = 20.20551,
            Longitude = -99.22178
        },
        new University
        {
            Code = "TESJI",
            Name = "Tecnológico de Estudios Superiores de Jilotepec",
            City = "Jilotepec",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM node 13173489446: campus.
            Latitude = 19.91953,
            Longitude = -99.57719
        },
        new University
        {
            Code = "UMB-JILO",
            Name = "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Jilotepec",
            City = "Jilotepec",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM node 13206892316: campus.
            Latitude = 19.91248,
            Longitude = -99.57849
        },
        new University
        {
            Code = "UMB-VDC",
            Name = "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Villa del Carbón",
            City = "Villa del Carbón",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM node 10900967466: centro de la localidad (aproximada).
            Latitude = 19.72791,
            Longitude = -99.46312
        },
        new University
        {
            Code = "UCAN",
            Name = "Universidad Canadiense",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM node 459391798: centro de la localidad (aproximada).
            Latitude = 20.0517,
            Longitude = -99.34507
        },
        new University
        {
            Code = "UNE-TULA",
            Name = "Universidad del Nuevo México",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt,
            // OSM way 458178077: campus.
            Latitude = 20.03108,
            Longitude = -99.33629
        }
    ];
}
