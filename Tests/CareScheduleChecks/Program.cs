using Google.Cloud.Firestore;
using HomePlant.Services;

var start = Timestamp.FromDateTime(new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));
var sevenDays = CareScheduleCalculator.NextFrom(start, 7);
Require(sevenDays.HasValue, "A frequency must create a next reminder.");
Require(
    sevenDays.Value.ToDateTime() == new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
    "The next reminder must advance by the configured number of days.");
Require(CareScheduleCalculator.NextFrom(start, null) == null, "An empty frequency must disable reminders.");

var preserved = CareScheduleCalculator.Recalculate(7, 7, sevenDays, null, start);
Require(preserved == sevenDays, "Saving an unchanged schedule must preserve its due date.");

var lastCare = Timestamp.FromDateTime(new DateTime(2026, 9, 14, 3, 30, 0, DateTimeKind.Utc));
var changed = CareScheduleCalculator.Recalculate(7, 10, sevenDays, lastCare, start);
Require(
    changed?.ToDateTime() == new DateTime(2026, 9, 24, 3, 30, 0, DateTimeKind.Utc),
    "A changed frequency must be based on the latest care activity.");

var firstId = CareScheduleCalculator.NotificationId("plant-1", "Watering", sevenDays.Value);
var secondId = CareScheduleCalculator.NotificationId("plant-1", "Watering", sevenDays.Value);
Require(firstId == secondId, "Notification IDs must be deterministic to prevent duplicates.");

Console.WriteLine("Care schedule checks passed.");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
