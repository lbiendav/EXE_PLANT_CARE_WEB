using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class CareReminderService
{
    private readonly FirestoreDb _db;
    private readonly EmailNotificationService _email;
    private readonly ILogger<CareReminderService> _logger;

    public CareReminderService(
        FirestoreService firestore,
        EmailNotificationService email,
        ILogger<CareReminderService> logger)
    {
        _db = firestore.Db;
        _email = email;
        _logger = logger;
    }

    private CollectionReference Notifications(string uid) =>
        _db.Collection("users").Document(uid).Collection("notifications");

    public bool EmailDeliveryAvailable => _email.IsConfigured;

    public async Task SyncForUser(
        string uid,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var userReference = _db.Collection("users").Document(uid);
        var user = await userReference.GetSnapshotAsync(cancellationToken);
        var emailEnabled = user.Exists &&
            user.TryGetValue<bool>("emailCareReminders", out var enabled) &&
            enabled;

        var plants = await _db.Collection("users")
            .Document(uid)
            .Collection("user_plants")
            .GetSnapshotAsync(cancellationToken);

        foreach (var document in plants.Documents)
        {
            try
            {
                var plant = document.ConvertTo<UserPlantModel>();
                await CreateDueNotifications(uid, emailEnabled ? email : null, plant, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not sync reminders for plant {PlantId}.", document.Id);
            }
        }
    }

    public async Task SyncAll(CancellationToken cancellationToken = default)
    {
        var plants = await _db.CollectionGroup("user_plants")
            .GetSnapshotAsync(cancellationToken);
        var userCache = new Dictionary<string, (string? Email, bool EmailEnabled)>();

        foreach (var document in plants.Documents)
        {
            var owner = document.Reference.Parent.Parent;
            if (owner == null || owner.Parent.Id != "users")
                continue;

            var uid = owner.Id;
            if (!userCache.TryGetValue(uid, out var userSettings))
            {
                var user = await owner.GetSnapshotAsync(cancellationToken);
                var email = user.Exists && user.TryGetValue<string>("email", out var address)
                    ? address
                    : null;
                var emailEnabled = user.Exists &&
                    user.TryGetValue<bool>("emailCareReminders", out var enabled) &&
                    enabled;
                userSettings = (email, emailEnabled);
                userCache[uid] = userSettings;
            }

            try
            {
                await CreateDueNotifications(
                    uid,
                    userSettings.EmailEnabled ? userSettings.Email : null,
                    document.ConvertTo<UserPlantModel>(),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not sync reminders for plant {PlantId}.", document.Id);
            }
        }
    }

    public async Task<List<CareNotificationModel>> GetAll(
        string uid,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await Notifications(uid)
            .OrderByDescending("dueAt")
            .Limit(100)
            .GetSnapshotAsync(cancellationToken);
        return snapshot.Documents
            .Select(document => document.ConvertTo<CareNotificationModel>())
            .ToList();
    }

    public async Task<int> GetUnreadCount(
        string uid,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await Notifications(uid)
            .WhereEqualTo("isRead", false)
            .GetSnapshotAsync(cancellationToken);
        return snapshot.Count;
    }

    public async Task MarkRead(
        string uid,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/'))
            return;

        var document = Notifications(uid).Document(id);
        if (!(await document.GetSnapshotAsync(cancellationToken)).Exists)
            return;

        await document.UpdateAsync(new Dictionary<string, object>
        {
            ["isRead"] = true,
            ["readAt"] = Timestamp.GetCurrentTimestamp()
        }, cancellationToken: cancellationToken);
    }

    public async Task MarkAllRead(
        string uid,
        CancellationToken cancellationToken = default)
    {
        var unread = await Notifications(uid)
            .WhereEqualTo("isRead", false)
            .GetSnapshotAsync(cancellationToken);
        if (unread.Count == 0)
            return;

        var now = Timestamp.GetCurrentTimestamp();
        foreach (var page in unread.Documents.Chunk(400))
        {
            var batch = _db.StartBatch();
            foreach (var document in page)
                batch.Update(document.Reference, new Dictionary<string, object>
                {
                    ["isRead"] = true,
                    ["readAt"] = now
                });
            await batch.CommitAsync(cancellationToken);
        }
    }

    public async Task<bool> GetEmailPreference(
        string uid,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Collection("users").Document(uid)
            .GetSnapshotAsync(cancellationToken);
        return user.Exists &&
            user.TryGetValue<bool>("emailCareReminders", out var enabled) &&
            enabled;
    }

    public Task SetEmailPreference(
        string uid,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        _db.Collection("users").Document(uid)
            .UpdateAsync("emailCareReminders", enabled, cancellationToken: cancellationToken);

    private async Task CreateDueNotifications(
        string uid,
        string? email,
        UserPlantModel plant,
        CancellationToken cancellationToken)
    {
        var now = Timestamp.GetCurrentTimestamp();
        var schedules = new[]
        {
            (Type: "Watering", Label: "tưới nước", DueAt: plant.NextWateringAt),
            (Type: "Fertilizing", Label: "bón phân", DueAt: plant.NextFertilizingAt),
            (Type: "Repotting", Label: "thay chậu", DueAt: plant.NextRepottingAt)
        };

        foreach (var schedule in schedules)
        {
            if (!schedule.DueAt.HasValue || schedule.DueAt.Value > now)
                continue;

            var dueAt = schedule.DueAt.Value;
            var notificationId = CareScheduleCalculator.NotificationId(plant.Id, schedule.Type, dueAt);
            var reference = Notifications(uid).Document(notificationId);
            var notification = new CareNotificationModel
            {
                UserId = uid,
                PlantId = plant.Id,
                PlantName = plant.CustomName,
                CareType = schedule.Type,
                Title = $"Đến lịch {schedule.Label}",
                Message = $"{plant.CustomName} đang đến lịch {schedule.Label}.",
                DueAt = dueAt,
                CreatedAt = now,
                EmailClaimedAt = string.IsNullOrWhiteSpace(email) ? null : now
            };

            var shouldSendEmail = await _db.RunTransactionAsync(async transaction =>
            {
                var existing = await transaction.GetSnapshotAsync(reference, cancellationToken);
                if (existing.Exists)
                {
                    if (string.IsNullOrWhiteSpace(email) ||
                        (existing.TryGetValue<Timestamp?>("emailSentAt", out var sentAt) && sentAt.HasValue))
                        return false;

                    if (existing.TryGetValue<Timestamp?>("emailClaimedAt", out var claimedAt) &&
                        claimedAt.HasValue &&
                        now.ToDateTime() - claimedAt.Value.ToDateTime() < TimeSpan.FromMinutes(10))
                        return false;

                    transaction.Update(reference, "emailClaimedAt", now);
                    return true;
                }
                transaction.Set(reference, notification);
                return !string.IsNullOrWhiteSpace(email);
            }, cancellationToken: cancellationToken);

            if (!shouldSendEmail || string.IsNullOrWhiteSpace(email))
                continue;

            if (await _email.SendCareReminder(
                email,
                $"HomePlant · {notification.Title}",
                notification.Message,
                cancellationToken))
            {
                await reference.UpdateAsync(new Dictionary<string, object>
                {
                    ["emailSentAt"] = Timestamp.GetCurrentTimestamp(),
                    ["emailClaimedAt"] = FieldValue.Delete
                }, cancellationToken: cancellationToken);
            }
            else
            {
                await reference.UpdateAsync(
                    "emailClaimedAt",
                    FieldValue.Delete,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
