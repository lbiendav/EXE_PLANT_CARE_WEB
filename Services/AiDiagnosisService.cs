using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class AiDiagnosisService
{
    private readonly FirestoreDb _db;

    public AiDiagnosisService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<AiDiagnosisModel>> GetAll()
    {
        var snapshot = await _db
            .Collection("ai_diagnoses")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<AiDiagnosisModel>())
            .ToList();
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("ai_diagnoses")
            .Document(id)
            .DeleteAsync();
    }
}
