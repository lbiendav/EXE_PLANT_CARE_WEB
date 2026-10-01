using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Distributed;

namespace HomePlant.Services;

/// <summary>
/// Persistent, multi-instance session storage using the existing production
/// Firestore dependency. Values are opaque Data Protection/session payloads and
/// documents are removed automatically by the expiresAt TTL policy.
/// </summary>
public sealed class FirestoreDistributedCache(FirestoreDb firestore) : IDistributedCache
{
    private CollectionReference Cache => firestore.Collection("system_cache");

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var reference = Cache.Document(Id(key));
        var snapshot = await reference.GetSnapshotAsync(token);
        if (!snapshot.Exists) return null;
        var now = DateTimeOffset.UtcNow;
        if (!snapshot.TryGetValue<Timestamp>("expiresAt", out var expiresAt) || expiresAt.ToDateTimeOffset() <= now)
        {
            await reference.DeleteAsync(cancellationToken: token);
            return null;
        }

        if (snapshot.TryGetValue<long>("slidingSeconds", out var slidingSeconds) && slidingSeconds > 0)
            await RefreshSnapshot(reference, snapshot, now, slidingSeconds, token);

        try
        {
            return Convert.FromBase64String(snapshot.GetValue<string>("value"));
        }
        catch (FormatException)
        {
            await reference.DeleteAsync(cancellationToken: token);
            return null;
        }
    }

    public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        var reference = Cache.Document(Id(key));
        var snapshot = await reference.GetSnapshotAsync(token);
        if (!snapshot.Exists || !snapshot.TryGetValue<long>("slidingSeconds", out var slidingSeconds) || slidingSeconds <= 0)
            return;
        await RefreshSnapshot(reference, snapshot, DateTimeOffset.UtcNow, slidingSeconds, token);
    }

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        Cache.Document(Id(key)).DeleteAsync(cancellationToken: token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        SetAsync(key, value, options).GetAwaiter().GetResult();

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var now = DateTimeOffset.UtcNow;
        var absolute = options.AbsoluteExpiration ??
            (options.AbsoluteExpirationRelativeToNow.HasValue ? now + options.AbsoluteExpirationRelativeToNow.Value : null);
        var sliding = options.SlidingExpiration;
        var expiresAt = sliding.HasValue ? now + sliding.Value : absolute ?? now.AddHours(12);
        if (absolute.HasValue && expiresAt > absolute.Value) expiresAt = absolute.Value;

        var data = new Dictionary<string, object>
        {
            ["value"] = Convert.ToBase64String(value),
            ["expiresAt"] = Timestamp.FromDateTimeOffset(expiresAt),
            ["absoluteExpiresAt"] = absolute.HasValue ? Timestamp.FromDateTimeOffset(absolute.Value) : FieldValue.Delete,
            ["slidingSeconds"] = sliding.HasValue ? checked((long)sliding.Value.TotalSeconds) : 0L,
            ["updatedAt"] = Timestamp.FromDateTimeOffset(now)
        };
        return Cache.Document(Id(key)).SetAsync(data, SetOptions.MergeAll, token);
    }

    private static async Task RefreshSnapshot(DocumentReference reference, DocumentSnapshot snapshot,
        DateTimeOffset now, long slidingSeconds, CancellationToken token)
    {
        var next = now.AddSeconds(slidingSeconds);
        if (snapshot.TryGetValue<Timestamp>("absoluteExpiresAt", out var absolute) && next > absolute.ToDateTimeOffset())
            next = absolute.ToDateTimeOffset();
        if (next <= now)
        {
            await reference.DeleteAsync(cancellationToken: token);
            return;
        }
        await reference.UpdateAsync(new Dictionary<string, object>
        {
            ["expiresAt"] = Timestamp.FromDateTimeOffset(next),
            ["updatedAt"] = Timestamp.FromDateTimeOffset(now)
        }, cancellationToken: token);
    }

    private static string Id(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
