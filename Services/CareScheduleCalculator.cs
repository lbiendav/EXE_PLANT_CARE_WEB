using Google.Cloud.Firestore;

namespace HomePlant.Services;

public static class CareScheduleCalculator
{
    private static readonly HashSet<string> Units =
        new(StringComparer.OrdinalIgnoreCase) { "Seconds", "Minutes", "Hours", "Days" };

    public static string NormalizeUnit(string? unit) =>
        Units.Contains(unit ?? "")
            ? char.ToUpperInvariant(unit![0]) + unit[1..].ToLowerInvariant()
            : "Days";

    public static string UnitLabel(string? unit) => NormalizeUnit(unit) switch
    {
        "Seconds" => "giây",
        "Minutes" => "phút",
        "Hours" => "giờ",
        _ => "ngày"
    };

    public static Timestamp? NextFrom(Timestamp start, int? frequency, string? unit = "Days")
    {
        if (!frequency.HasValue)
            return null;

        var startAt = start.ToDateTime();
        var nextAt = NormalizeUnit(unit) switch
        {
            "Seconds" => startAt.AddSeconds(frequency.Value),
            "Minutes" => startAt.AddMinutes(frequency.Value),
            "Hours" => startAt.AddHours(frequency.Value),
            _ => startAt.AddDays(frequency.Value)
        };
        return Timestamp.FromDateTime(nextAt);
    }

    public static Timestamp? Recalculate(
        int? previousFrequency,
        string? previousUnit,
        int? frequency,
        string? unit,
        Timestamp? previousNext,
        Timestamp? lastCare,
        Timestamp now)
    {
        if (!frequency.HasValue)
            return null;
        if (frequency == previousFrequency &&
            NormalizeUnit(unit) == NormalizeUnit(previousUnit) &&
            previousNext.HasValue)
            return previousNext;

        return NextFrom(lastCare ?? now, frequency, unit);
    }

    public static string NotificationId(string plantId, string careType, Timestamp dueAt)
    {
        var dueUnixSeconds = new DateTimeOffset(dueAt.ToDateTime()).ToUnixTimeSeconds();
        return $"{plantId}-{careType.ToLowerInvariant()}-{dueUnixSeconds}";
    }
}
