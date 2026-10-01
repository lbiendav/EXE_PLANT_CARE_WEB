using Google.Cloud.Firestore;

namespace HomePlant.Services;

/// <summary>
/// Removes expired distributed-session documents without requiring Firestore's
/// billing-dependent TTL feature. Work is bounded so cleanup cannot monopolize
/// the database if a deployment has accumulated a large backlog.
/// </summary>
public sealed class FirestoreCacheCleanupBackgroundService(
    FirestoreDb firestore,
    ILogger<FirestoreCacheCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private const int BatchSize = 200;
    private const int MaxBatchesPerRun = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var removed = await CleanupOnce(stoppingToken);
                    if (removed > 0)
                        logger.LogInformation("Removed {Count} expired session cache documents.", removed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Expired session cache cleanup failed.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    public async Task<int> CleanupOnce(CancellationToken cancellationToken = default)
    {
        var removed = 0;
        for (var batchNumber = 0; batchNumber < MaxBatchesPerRun; batchNumber++)
        {
            var expired = await firestore.Collection("system_cache")
                .WhereLessThanOrEqualTo("expiresAt", Timestamp.GetCurrentTimestamp())
                .Limit(BatchSize)
                .GetSnapshotAsync(cancellationToken);
            if (expired.Count == 0)
                break;

            var batch = firestore.StartBatch();
            foreach (var document in expired.Documents)
                batch.Delete(document.Reference);
            await batch.CommitAsync(cancellationToken);
            removed += expired.Count;

            if (expired.Count < BatchSize)
                break;
        }
        return removed;
    }
}
