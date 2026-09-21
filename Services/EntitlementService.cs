using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class EntitlementService(
    FirestoreService firestore,
    PaymentModePolicy paymentPolicy,
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
        var demoAllowed = paymentPolicy.IsDemoSubscriptionAllowed(subscription);
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
