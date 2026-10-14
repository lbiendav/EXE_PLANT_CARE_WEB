using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace HomePlant.Services;

public sealed record PlanDisplaySetting(bool Enabled, string Description, string Benefits, string Version);

public sealed class PlanSettingsService(FirestoreService firestore, IMemoryCache? cache = null)
{
    private const string CacheKey = "HomePlant:PlanSettings:All";

    public async Task<IReadOnlyDictionary<string, PlanDisplaySetting>> GetAll()
    {
        if (cache != null && cache.TryGetValue<IReadOnlyDictionary<string, PlanDisplaySetting>>(CacheKey, out var cached) && cached != null)
            return cached;

        var snapshot = await firestore.Db.Collection("plan_settings").GetSnapshotAsync();
        var result = snapshot.Documents.ToDictionary(x => x.Id, x => new PlanDisplaySetting(
            !x.TryGetValue<bool>("enabled", out var enabled) || enabled,
            x.TryGetValue<string>("description", out var description) ? description : "",
            x.TryGetValue<string>("benefits", out var benefits) ? benefits : "",
            x.TryGetValue<string>("version", out var version) ? version : PlanCatalogService.Version), StringComparer.OrdinalIgnoreCase);

        cache?.Set(CacheKey, result, TimeSpan.FromMinutes(30));
        return result;
    }
}
