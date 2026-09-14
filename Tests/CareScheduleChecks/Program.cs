using Google.Cloud.Firestore;
using HomePlant.Models;
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

var aiRecommendations = new AiCareRecommendationsModel
{
    IsSuitableForAutomation = true,
    Watering = new AiCareFrequencyModel { Frequency = 7, Unit = "Days" },
    Fertilizing = new AiCareFrequencyModel { Frequency = 30, Unit = "Days" },
    Repotting = new AiCareFrequencyModel { Frequency = 365, Unit = "Days" }
};
Require(
    CareScheduleCalculator.TryCreateSchedule(aiRecommendations, out var aiSchedule),
    "A safe, in-range AI recommendation must create a care schedule.");
Require(
    aiSchedule.WateringFrequency == 7 &&
    aiSchedule.FertilizingFrequency == 30 &&
    aiSchedule.RepottingFrequency == 365,
    "The AI schedule must preserve all recommended frequencies.");

aiRecommendations.IsSuitableForAutomation = false;
Require(
    !CareScheduleCalculator.TryCreateSchedule(aiRecommendations, out _),
    "An uncertain AI recommendation must not be eligible for automatic scheduling.");

aiRecommendations.IsSuitableForAutomation = true;
aiRecommendations.Watering.Frequency = 0;
Require(
    !CareScheduleCalculator.TryCreateSchedule(aiRecommendations, out _),
    "An out-of-range AI frequency must be rejected server-side.");

Console.WriteLine("Care schedule checks passed.");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
