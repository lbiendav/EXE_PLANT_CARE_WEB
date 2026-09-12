using Google.Cloud.Firestore;
using HomePlant.Services;

var start = Timestamp.FromDateTime(new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));
var sevenDays = CareScheduleCalculator.NextFrom(start, 7, "Days")
    ?? throw new InvalidOperationException("A frequency must create a next reminder.");
Require(
    sevenDays.ToDateTime() == new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
    "The next reminder must advance by the configured number of days.");
Require(CareScheduleCalculator.NextFrom(start, null) == null, "An empty frequency must disable reminders.");

var preserved = CareScheduleCalculator.Recalculate(7, "Days", 7, "Days", sevenDays, null, start);
Require(preserved == sevenDays, "Saving an unchanged schedule must preserve its due date.");

var lastCare = Timestamp.FromDateTime(new DateTime(2026, 9, 14, 3, 30, 0, DateTimeKind.Utc));
var changed = CareScheduleCalculator.Recalculate(7, "Days", 10, "Days", sevenDays, lastCare, start);
Require(
    changed?.ToDateTime() == new DateTime(2026, 9, 24, 3, 30, 0, DateTimeKind.Utc),
    "A changed frequency must be based on the latest care activity.");

var thirtySeconds = CareScheduleCalculator.NextFrom(start, 30, "Seconds");
Require(
    thirtySeconds?.ToDateTime() == new DateTime(2026, 9, 12, 0, 0, 30, DateTimeKind.Utc),
    "Second-based reminders must keep second precision.");
var twoMinutes = CareScheduleCalculator.NextFrom(start, 2, "Minutes");
Require(
    twoMinutes?.ToDateTime() == new DateTime(2026, 9, 12, 0, 2, 0, DateTimeKind.Utc),
    "Minute-based reminders must keep minute precision.");
var changedUnit = CareScheduleCalculator.Recalculate(7, "Days", 7, "Minutes", sevenDays, null, start);
Require(
    changedUnit?.ToDateTime() == new DateTime(2026, 9, 12, 0, 7, 0, DateTimeKind.Utc),
    "Changing only the unit must recalculate the reminder.");

var firstId = CareScheduleCalculator.NotificationId("plant-1", "Watering", sevenDays);
var secondId = CareScheduleCalculator.NotificationId("plant-1", "Watering", sevenDays);
Require(firstId == secondId, "Notification IDs must be deterministic to prevent duplicates.");

Console.WriteLine("Care schedule checks passed.");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
