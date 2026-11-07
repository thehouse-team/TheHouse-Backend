using System.Globalization;
using System.Text.RegularExpressions;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.Common;

// NV-17 §6/§9: reglas de horario de una solicitud de visita.
//  - `scheduledAt` en ISO 8601 con desplazamiento explícito (400 si falta).
//  - De 2 horas a 60 días en el futuro, entre 08:00 y 20:00 de
//    America/Mexico_City, en múltiplos de 15 minutos (422 si no).
public static partial class VisitSchedulePolicy
{
    public const string TimeZoneId = "America/Mexico_City";

    public static readonly TimeSpan MinimumLead = TimeSpan.FromHours(2);

    public static readonly TimeSpan MaximumLead = TimeSpan.FromDays(60);

    public static readonly TimeSpan OpeningTime = TimeSpan.FromHours(8);

    public static readonly TimeSpan ClosingTime = TimeSpan.FromHours(20);

    public static readonly TimeSpan ConflictWindow = TimeSpan.FromMinutes(60);

    // México no usa horario de verano desde 2022: si el sistema no tiene la
    // base de zonas horarias, se usa UTC-06:00 fijo con el mismo id.
    public static readonly TimeZoneInfo MexicoCity = ResolveTimeZone();

    public static DateTimeOffset ParseScheduledAt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !ExplicitOffset().IsMatch(value) ||
            !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new RequestValidationException(
                "scheduledAt",
                "scheduledAt must be ISO 8601 with an explicit offset.");
        }

        return parsed.ToUniversalTime();
    }

    public static void EnsureAllowed(DateTimeOffset scheduledAtUtc, DateTimeOffset now)
    {
        if (scheduledAtUtc < now + MinimumLead)
        {
            throw new BusinessRuleException("too_soon");
        }

        if (scheduledAtUtc > now + MaximumLead)
        {
            throw new BusinessRuleException("too_far");
        }

        var local = TimeZoneInfo.ConvertTime(scheduledAtUtc, MexicoCity);

        if (local.Second != 0 || local.Millisecond != 0 || local.Minute % 15 != 0)
        {
            throw new BusinessRuleException("not_quarter_hour");
        }

        if (local.TimeOfDay < OpeningTime || local.TimeOfDay > ClosingTime)
        {
            throw new BusinessRuleException("outside_hours");
        }
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                TimeZoneId,
                TimeSpan.FromHours(-6),
                TimeZoneId,
                TimeZoneId);
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}
