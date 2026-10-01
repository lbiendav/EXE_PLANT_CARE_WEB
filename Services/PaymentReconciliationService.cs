using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class PaymentReconciliationService(
    FirestoreService firestore,
    IPaymentProvider provider,
    LivePaymentService livePayments,
    ISubscriptionClock clock,
    ILogger<PaymentReconciliationService> logger)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<int> ReconcilePending(CancellationToken cancellationToken)
    {
        var snapshot = await _db.Collection("subscription_orders")
            .WhereEqualTo("isDemo", false)
            .WhereEqualTo("status", "Pending")
            .OrderBy("createdAt")
            .Limit(50)
            .GetSnapshotAsync(cancellationToken);
        var reconciled = 0;
        foreach (var document in snapshot.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = document.ConvertTo<SubscriptionOrderModel>();
            if (order.ProviderOrderCode <= 0 || order.CreatedAt.ToDateTimeOffset() > clock.UtcNow.AddMinutes(-2))
                continue;
            if (document.TryGetValue<Timestamp>("lastSyncedAt", out var lastSynced) &&
                lastSynced.ToDateTimeOffset() > clock.UtcNow.AddMinutes(-5))
                continue;

            try
            {
                var state = await provider.GetCheckout(order.ProviderOrderCode, cancellationToken);
                if (state.OrderCode != order.ProviderOrderCode ||
                    (!string.IsNullOrWhiteSpace(order.ProviderPaymentLinkId) && state.PaymentLinkId != order.ProviderPaymentLinkId))
                {
                    logger.LogError("Payment reconciliation mismatch for order {OrderId}.", order.Id);
                    continue;
                }

                if (state.Payment != null)
                    await livePayments.Reprocess(state.Payment);

                var updates = new Dictionary<string, object>
                {
                    ["rawProviderStatus"] = state.Status,
                    ["lastSyncedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow)
                };
                if (string.IsNullOrWhiteSpace(order.ProviderPaymentLinkId) && !string.IsNullOrWhiteSpace(state.PaymentLinkId))
                {
                    updates["providerPaymentLinkId"] = state.PaymentLinkId;
                    updates["checkoutStatus"] = "Open";
                }
                await document.Reference.UpdateAsync(updates, cancellationToken: cancellationToken);
                reconciled++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Payment reconciliation failed for order {OrderId}.", order.Id);
            }
        }
        return reconciled;
    }
}
