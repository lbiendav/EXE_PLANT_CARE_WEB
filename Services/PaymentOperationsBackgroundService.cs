using Google.Cloud.Firestore;

namespace HomePlant.Services;

public sealed class PaymentOperationsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PaymentOperationsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!PaymentModePolicy.ParseMode(configuration["Payments:Mode"]).Equals(PaymentRuntimeMode.Live))
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await DeliverReceipts(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Receipt delivery is deliberately isolated from provider
                // reconciliation. A missing index or temporary payOS failure must
                // never prevent an already verified customer from receiving email.
                logger.LogError(exception, "Payment receipt delivery cycle failed.");
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<PaymentReconciliationService>();
                await reconciler.ReconcilePending(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment reconciliation cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static async Task DeliverReceipts(IServiceProvider services, CancellationToken cancellationToken)
    {
        var firestore = services.GetRequiredService<FirestoreService>().Db;
        var receipts = services.GetRequiredService<PaymentReceiptService>();
        var pending = await firestore.Collection("payment_receipt_deliveries")
            .WhereEqualTo("status", "Pending")
            .Limit(25)
            .GetSnapshotAsync(cancellationToken);
        foreach (var delivery in pending.Documents)
            await receipts.SendOnce(delivery.Id, cancellationToken);

        var failedDue = await firestore.Collection("payment_receipt_deliveries")
            .WhereEqualTo("status", "Failed")
            .WhereLessThanOrEqualTo("nextAttemptAt", Timestamp.GetCurrentTimestamp())
            .OrderBy("nextAttemptAt")
            .Limit(25)
            .GetSnapshotAsync(cancellationToken);
        foreach (var delivery in failedDue.Documents)
            await receipts.SendOnce(delivery.Id, cancellationToken);
    }
}
