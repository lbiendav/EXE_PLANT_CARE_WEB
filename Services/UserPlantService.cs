using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class UserPlantService
{
    private readonly FirestoreDb _db;

    public UserPlantService(FirestoreService firestore)
    {
        _db = firestore.Db;
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
        await Collection(uid).AddAsync(plant);
    }

    public async Task Update(string uid, string id, UserPlantModel plant)
    {
        await Collection(uid).Document(id).SetAsync(plant);
    }

    public async Task Delete(string uid, string id)
    {
        // A missing/unowned parent must not authorize deletion of global care logs.
        if (!(await Collection(uid).Document(id).GetSnapshotAsync()).Exists) return;
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

        await Collection(uid).Document(id).DeleteAsync();
    }

    public async Task<int> CountAll()
    {
        var snapshot = await _db
            .CollectionGroup("user_plants")
            .GetSnapshotAsync();

        return snapshot.Count;
    }
}
