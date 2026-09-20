namespace HomePlant.Services;

public interface ISubscriptionClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemSubscriptionClock : ISubscriptionClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class SubscriptionTime
{
    public static readonly TimeZoneInfo Vietnam = ResolveVietnamTimeZone();

    public static DateTimeOffset AddCalendarMonths(DateTimeOffset instant, int months)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Vietnam);
        var unspecified = DateTime.SpecifyKind(local.DateTime.AddMonths(months), DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Vietnam.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    public static string UsageMonth(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Vietnam).ToString("yyyy-MM");

    public static DateTimeOffset NextUsageReset(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Vietnam);
        var next = new DateTime(local.Year, local.Month, 1).AddMonths(1);
        return new DateTimeOffset(next, Vietnam.GetUtcOffset(next)).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}
