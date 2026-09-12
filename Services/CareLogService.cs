using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class CareLogService
{
    private readonly FirestoreDb _db;

    public CareLogService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<CareLogModel>> GetLogs(
        string plantId)
    {
        var snapshot = await _db
            .Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .OrderByDescending("CreatedAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<CareLogModel>())
            .ToList();
    }

    public async Task AddLog(
        string uid,
        string plantId,
        CareLogModel log)
    {
        var plantReference = _db.Collection("users")
            .Document(uid)
            .Collection("user_plants")
            .Document(plantId);
        var plantSnapshot = await plantReference.GetSnapshotAsync();
        if (!plantSnapshot.Exists)
            throw new InvalidOperationException("Plant does not exist.");

        var plant = plantSnapshot.ConvertTo<UserPlantModel>();
        var logReference = _db.Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .Document();
        var updates = CareUpdates(plant, log.ActionType, log.CreatedAt);
        var notifications = await _db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .GetSnapshotAsync();
        var batch = _db.StartBatch();
        batch.Set(logReference, log);
        if (updates.Count > 0)
            batch.Update(plantReference, updates);
        foreach (var notification in notifications.Documents
            .Where(document =>
                document.TryGetValue<string>("plantId", out var notifiedPlantId) &&
                notifiedPlantId == plantId &&
                document.TryGetValue<string>("careType", out var careType) &&
                careType == log.ActionType &&
                (!document.TryGetValue<bool>("isRead", out var isRead) || !isRead))
            .Take(400))
        {
            batch.Update(notification.Reference, new Dictionary<string, object>
            {
                ["isRead"] = true,
                ["readAt"] = log.CreatedAt
            });
        }
        await batch.CommitAsync();
    }

    public async Task DeleteLog(
        string uid,
        string plantId,
        string logId)
    {
        var logReference = _db.Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .Document(logId);
        var deletedSnapshot = await logReference.GetSnapshotAsync();
        if (!deletedSnapshot.Exists)
            return;

        var deleted = deletedSnapshot.ConvertTo<CareLogModel>();
        var plantReference = _db.Collection("users")
            .Document(uid)
            .Collection("user_plants")
            .Document(plantId);
        var plantSnapshot = await plantReference.GetSnapshotAsync();
        if (!plantSnapshot.Exists)
            return;

        var plant = plantSnapshot.ConvertTo<UserPlantModel>();
        var remaining = await GetLogs(plantId);
        var latest = remaining
            .Where(log => log.Id != logId && log.ActionType == deleted.ActionType)
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();
        var updates = CareUpdates(
            plant,
            deleted.ActionType,
            latest?.CreatedAt,
            usePlantedAtWhenMissing: true);

        var batch = _db.StartBatch();
        batch.Delete(logReference);
        if (updates.Count > 0)
            batch.Update(plantReference, updates);
        await batch.CommitAsync();
    }

    private static Dictionary<string, object> CareUpdates(
        UserPlantModel plant,
        string actionType,
        Timestamp? caredAt,
        bool usePlantedAtWhenMissing = false)
    {
        var updates = new Dictionary<string, object>();
        string? lastField = null;
        string? nextField = null;
        int? frequency = null;

        switch (actionType)
        {
            case "Watering":
                lastField = "lastWatered";
                nextField = "nextWateringAt";
                frequency = plant.WateringFrequency;
                break;
            case "Fertilizing":
                lastField = "lastFertilized";
                nextField = "nextFertilizingAt";
                frequency = plant.FertilizingFrequency;
                break;
            case "Repotting":
                lastField = "lastRepotted";
                nextField = "nextRepottingAt";
                frequency = plant.RepottingFrequency;
                break;
        }

        if (lastField == null || nextField == null)
            return updates;

        updates[lastField] = caredAt.HasValue ? caredAt.Value : null!;
        var scheduleBase = caredAt ?? (usePlantedAtWhenMissing ? plant.PlantedAt : null);
        updates[nextField] = scheduleBase.HasValue
            ? CareScheduleCalculator.NextFrom(scheduleBase.Value, frequency) ?? null!
            : null!;
        return updates;
    }
}
