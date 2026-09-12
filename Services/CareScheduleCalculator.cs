using Google.Cloud.Firestore;

namespace HomePlant.Services;

public static class CareScheduleCalculator
{
    public static Timestamp? NextFrom(Timestamp start, int? frequency) =>
        frequency.HasValue
            ? Timestamp.FromDateTime(start.ToDateTime().AddDays(frequency.Value))
            : null;

    public static Timestamp? Recalculate(
        int? previousFrequency,
        int? frequency,
        Timestamp? previousNext,
        Timestamp? lastCare,
        Timestamp now)
    {
        if (!frequency.HasValue)
            return null;
        if (frequency == previousFrequency && previousNext.HasValue)
            return previousNext;

        return NextFrom(lastCare ?? now, frequency);
    }

    public static string NotificationId(string plantId, string careType, Timestamp dueAt)
    {
        var dueUnixSeconds = new DateTimeOffset(dueAt.ToDateTime()).ToUnixTimeSeconds();
        return $"{plantId}-{careType.ToLowerInvariant()}-{dueUnixSeconds}";
    }
}
