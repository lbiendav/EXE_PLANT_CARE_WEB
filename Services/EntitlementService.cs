using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class EntitlementService(
    FirestoreService firestore,
    IConfiguration configuration,
    ISubscriptionClock clock)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<CurrentEntitlement> Get(string uid, CancellationToken cancellationToken = default)
    {
        var snapshot = await _db.Collection("subscriptions").Document(uid).GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists)
            return new CurrentEntitlement(PlanCatalogService.Free, null, false);

        var subscription = snapshot.ConvertTo<SubscriptionModel>();
        var now = clock.UtcNow;
        var stage = configuration["App:DeploymentStage"] ?? "Production";
        var demoAllowed = !subscription.IsDemo || !stage.Equals("Production", StringComparison.OrdinalIgnoreCase);
        var active = demoAllowed && subscription.StartsAt.ToDateTimeOffset() <= now && now < subscription.ExpiresAt.ToDateTimeOffset();
        if (!active)
            return new CurrentEntitlement(PlanCatalogService.Free, subscription, false);

        var entitlements = new EntitlementSnapshot(
            subscription.Tier,
            subscription.PlantLimit,
            subscription.MonthlyAiLimit,
            subscription.CatalogVersion);
        return new CurrentEntitlement(entitlements, subscription, true);
    }
}
