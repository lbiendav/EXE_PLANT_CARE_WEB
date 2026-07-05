using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class PlantTemplateService
{
    private readonly FirestoreDb _db;

    public PlantTemplateService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<PlantTemplateModel>> GetAll()
    {
        var snapshot = await _db.Collection("plant_templates").GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<PlantTemplateModel>())
            .ToList();
    }

    public async Task<PlantTemplateModel?> GetById(string id)
    {
        var doc = await _db.Collection("plant_templates").Document(id).GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<PlantTemplateModel>();
    }

    public async Task Delete(string id)
    {
        await _db.Collection("plant_templates").Document(id).DeleteAsync();
    }
}
