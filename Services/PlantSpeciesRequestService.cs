using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class PlantSpeciesRequestService
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "species_requests";

    public PlantSpeciesRequestService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<PlantSpeciesRequestModel>> GetAll(string? status = null)
    {
        Query query = _db.Collection(CollectionName);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.WhereEqualTo("status", status);
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents
            .Select(d => d.ConvertTo<PlantSpeciesRequestModel>())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task<PlantSpeciesRequestModel?> GetById(string id)
    {
        if (!FirestoreDocumentId.IsValid(id))
            return null;

        var doc = await _db.Collection(CollectionName).Document(id).GetSnapshotAsync();
        return doc.Exists ? doc.ConvertTo<PlantSpeciesRequestModel>() : null;
    }

    public async Task<int> CountPending()
    {
        var countSnapshot = await _db.Collection(CollectionName)
            .WhereEqualTo("status", "pending")
            .Count()
            .GetSnapshotAsync();
        return checked((int)(countSnapshot.Count ?? 0));
    }

    public async Task<string> Add(PlantSpeciesRequestModel request)
    {
        request.CreatedAt = Timestamp.GetCurrentTimestamp();
        var docRef = await _db.Collection(CollectionName).AddAsync(request);
        request.Id = docRef.Id;
        return docRef.Id;
    }

    public async Task Update(string id, PlantSpeciesRequestModel request)
    {
        request.UpdatedAt = Timestamp.GetCurrentTimestamp();
        await _db.Collection(CollectionName).Document(id).SetAsync(request, SetOptions.MergeAll);
    }

    public async Task Delete(string id)
    {
        await _db.Collection(CollectionName).Document(id).DeleteAsync();
    }
}
