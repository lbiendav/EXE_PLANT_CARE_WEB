using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class UserPlantService
{
    private readonly FirestoreDb _db;
    private readonly IConfiguration _configuration;
    private readonly ISubscriptionClock _clock;
    private readonly PaymentModePolicy _paymentPolicy;

    public UserPlantService(FirestoreService firestore, IConfiguration configuration, ISubscriptionClock clock, PaymentModePolicy paymentPolicy)
    {
        _db = firestore.Db;
        _configuration = configuration;
        _clock = clock;
        _paymentPolicy = paymentPolicy;
    }

    private CollectionReference Collection(string uid) =>
        _db.Collection("users").Document(uid).Collection("user_plants");

    public async Task<List<UserPlantModel>> GetAll(string uid)
    {
        var snapshot = await Collection(uid).GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<UserPlantModel>())
            .ToList();
    }

    public async Task<UserPlantModel?> GetById(string uid, string id)
    {
        var doc = await Collection(uid).Document(id).GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<UserPlantModel>();
    }

    public async Task Add(string uid, UserPlantModel plant)
    {
        if (!(_configuration.GetValue<bool?>("Subscriptions:EnforceLimits") ?? false))
        {
            var document = Collection(uid).Document();
            plant.Id = document.Id;
            await document.SetAsync(plant);
            return;
        }

        var initialCount = await ReadPlantCount(uid);
        var plantRef = Collection(uid).Document();
        var counterRef = _db.Collection("users").Document(uid).Collection("usage_state").Document("current");
        var subscriptionRef = _db.Collection("subscriptions").Document(uid);
        await _db.RunTransactionAsync(async transaction =>
        {
            var counter = await transaction.GetSnapshotAsync(counterRef);
            var subscription = await transaction.GetSnapshotAsync(subscriptionRef);
            var count = counter.Exists && counter.TryGetValue<int>("plantCount", out var storedCount)
                ? storedCount
                : initialCount;
            var limit = EffectivePlantLimit(subscription);
            if (count >= limit)
                throw new PlantLimitException($"Gói hiện tại cho phép tối đa {limit} cây.");
            plant.Id = plantRef.Id;
            transaction.Set(plantRef, plant);
            transaction.Set(counterRef, new Dictionary<string, object> { ["plantCount"] = count + 1, ["updatedAt"] = Timestamp.FromDateTimeOffset(_clock.UtcNow) }, SetOptions.MergeAll);
        });
    }

    public async Task EnsureCanAdd(string uid)
    {
        if (!(_configuration.GetValue<bool?>("Subscriptions:EnforceLimits") ?? false)) return;
        var counterTask = _db.Collection("users").Document(uid).Collection("usage_state").Document("current").GetSnapshotAsync();
        var subscriptionTask = _db.Collection("subscriptions").Document(uid).GetSnapshotAsync();
        await Task.WhenAll(counterTask, subscriptionTask);
        var count = counterTask.Result.Exists && counterTask.Result.TryGetValue<int>("plantCount", out var storedCount)
            ? storedCount
            : (await Collection(uid).GetSnapshotAsync()).Count;
        var limit = EffectivePlantLimit(subscriptionTask.Result);
        if (count >= limit) throw new PlantLimitException($"Gói hiện tại cho phép tối đa {limit} cây.");
    }

    public async Task Update(string uid, string id, UserPlantModel plant)
    {
        var plantRef = Collection(uid).Document(id);
        await _db.RunTransactionAsync(async transaction =>
        {
            if (!(await transaction.GetSnapshotAsync(plantRef)).Exists)
                throw new InvalidOperationException("Cây không còn tồn tại trong vườn.");
            transaction.Set(plantRef, plant);
        });
    }

    public async Task Delete(string uid, string id)
    {
        var plantRef = Collection(uid).Document(id);
        if (_configuration.GetValue<bool?>("Subscriptions:EnforceLimits") ?? false)
        {
            var initialCount = await ReadPlantCount(uid);
            var counterRef = _db.Collection("users").Document(uid).Collection("usage_state").Document("current");
            var cleanupRef = _db.Collection("users").Document(uid).Collection("plant_cleanup").Document(id);
            var deleted = await _db.RunTransactionAsync(async transaction =>
            {
                var plantSnapshot = await transaction.GetSnapshotAsync(plantRef);
                var counterSnapshot = await transaction.GetSnapshotAsync(counterRef);
                var cleanupSnapshot = await transaction.GetSnapshotAsync(cleanupRef);
                if (!plantSnapshot.Exists) return cleanupSnapshot.Exists;
                var count = counterSnapshot.Exists && counterSnapshot.TryGetValue<int>("plantCount", out var storedCount)
                    ? storedCount
                    : Math.Max(1, initialCount);
                transaction.Delete(plantRef);
                transaction.Set(counterRef, new Dictionary<string, object> { ["plantCount"] = Math.Max(0, count - 1), ["updatedAt"] = Timestamp.FromDateTimeOffset(_clock.UtcNow) }, SetOptions.MergeAll);
                transaction.Set(cleanupRef, new { plantId = id, createdAt = Timestamp.FromDateTimeOffset(_clock.UtcNow) });
                return true;
            });
            if (!deleted) return;
        }
        else
        {
            if (!(await plantRef.GetSnapshotAsync()).Exists) return;
            await plantRef.DeleteAsync();
        }

        var logs = _db.Collection("plants").Document(id).Collection("careLogs");
        while (true)
        {
            var page = await logs.Limit(100).GetSnapshotAsync();
            if (page.Count == 0) break;
            var batch = _db.StartBatch();
            foreach (var doc in page.Documents) batch.Delete(doc.Reference);
            await batch.CommitAsync();
        }

        var notifications = _db.Collection("users")
            .Document(uid)
            .Collection("notifications")
            .WhereEqualTo("plantId", id);
        while (true)
        {
            var page = await notifications.Limit(100).GetSnapshotAsync();
            if (page.Count == 0) break;
            var batch = _db.StartBatch();
            foreach (var doc in page.Documents) batch.Delete(doc.Reference);
            await batch.CommitAsync();
        }
        if (_configuration.GetValue<bool?>("Subscriptions:EnforceLimits") ?? false)
            await _db.Collection("users").Document(uid).Collection("plant_cleanup").Document(id).DeleteAsync();
    }

    private int EffectivePlantLimit(DocumentSnapshot snapshot)
    {
        if (!snapshot.Exists) return PlanCatalogService.Free.PlantLimit;
        var subscription = snapshot.ConvertTo<SubscriptionModel>();
        var now = _clock.UtcNow;
        var demoAllowed = _paymentPolicy.IsDemoSubscriptionAllowed(subscription);
        return demoAllowed && subscription.StartsAt.ToDateTimeOffset() <= now && now < subscription.ExpiresAt.ToDateTimeOffset()
            ? subscription.PlantLimit : PlanCatalogService.Free.PlantLimit;
    }

    private async Task<int> ReadPlantCount(string uid)
    {
        var state = await _db.Collection("users").Document(uid)
            .Collection("usage_state").Document("current").GetSnapshotAsync();
        if (state.Exists && state.TryGetValue<int>("plantCount", out var count))
            return count;
        return (await Collection(uid).GetSnapshotAsync()).Count;
    }

    public async Task<int> CountAll()
    {
        var snapshot = await _db
            .CollectionGroup("user_plants")
            .GetSnapshotAsync();

        return snapshot.Count;
    }

    public async Task<List<OwnedUserPlant>> GetAllForAdmin()
    {
        var snapshot = await _db
            .CollectionGroup("user_plants")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(document => new OwnedUserPlant(
                document.Reference.Parent.Parent?.Id ?? "",
                document.ConvertTo<UserPlantModel>()))
            .ToList();
    }
}

public sealed class PlantLimitException(string message) : Exception(message);

public sealed record OwnedUserPlant(string UserId, UserPlantModel Plant);
