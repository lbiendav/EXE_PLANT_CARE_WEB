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

    public async Task Delete(string uid, string id)
    {
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
