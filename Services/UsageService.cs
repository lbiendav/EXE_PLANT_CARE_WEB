using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class UsageService(FirestoreService firestore, ISubscriptionClock clock)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<SubscriptionUsage> Get(string uid, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var plantsTask = _db.Collection("users").Document(uid).Collection("user_plants").GetSnapshotAsync(cancellationToken);
        var usageTask = _db.Collection("users").Document(uid).Collection("usage")
            .Document(SubscriptionTime.UsageMonth(now)).GetSnapshotAsync(cancellationToken);
        await Task.WhenAll(plantsTask, usageTask);
        var usage = usageTask.Result.Exists ? usageTask.Result.ConvertTo<UsageMonthModel>() : new UsageMonthModel();
        return new SubscriptionUsage(plantsTask.Result.Count, usage.AiUsed, usage.AiReserved, SubscriptionTime.NextUsageReset(now));
    }
}
