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
            Code = "UAEH-ESTE",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tepeji del Río",
            City = "Tepeji del Río de Ocampo",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UAEH-ESTL",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tlahuelilpan",
            City = "Tlahuelilpan",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UAEH-ESAT",
            Name = "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula",
            City = "Atotonilco de Tula",
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
        },
        new University
        {
            Code = "TESJI",
            Name = "Tecnológico de Estudios Superiores de Jilotepec",
            City = "Jilotepec",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UMB-JILO",
            Name = "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Jilotepec",
            City = "Jilotepec",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UMB-VDC",
            Name = "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Villa del Carbón",
            City = "Villa del Carbón",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UCAN",
            Name = "Universidad Canadiense",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt
        },
        new University
        {
            Code = "UNE-TULA",
            Name = "Universidad del Nuevo México",
            City = "Tula de Allende",
            Country = "México",
            CreatedAt = SeedCreatedAt
        }
    ];
}
