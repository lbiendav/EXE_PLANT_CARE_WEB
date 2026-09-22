using Google.Cloud.Firestore;

namespace HomePlant.Services;

public sealed record PlanDisplaySetting(bool Enabled, string Description, string Benefits, string Version);

public sealed class PlanSettingsService(FirestoreService firestore)
{
    public async Task<IReadOnlyDictionary<string, PlanDisplaySetting>> GetAll()
    {
        var snapshot = await firestore.Db.Collection("plan_settings").GetSnapshotAsync();
        return snapshot.Documents.ToDictionary(x => x.Id, x => new PlanDisplaySetting(
            !x.TryGetValue<bool>("enabled", out var enabled) || enabled,
            x.TryGetValue<string>("description", out var description) ? description : "",
            x.TryGetValue<string>("benefits", out var benefits) ? benefits : "",
            x.TryGetValue<string>("version", out var version) ? version : PlanCatalogService.Version), StringComparer.OrdinalIgnoreCase);
    }
}
