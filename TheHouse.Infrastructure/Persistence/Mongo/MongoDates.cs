namespace TheHouse.Infrastructure.Persistence.Mongo;

internal static class MongoDates
{
    public static DateTime ToUtc(DateTimeOffset value) => value.UtcDateTime;

    public static DateTimeOffset FromUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
