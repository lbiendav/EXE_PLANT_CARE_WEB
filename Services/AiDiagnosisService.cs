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

    public async Task<List<AiDiagnosisModel>> GetByUser(string userId)
    {
        var snapshot = await _db.Collection("ai_diagnoses")
            .WhereEqualTo("userId", userId)
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<AiDiagnosisModel>())
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task<AiDiagnosisModel?> GetByIdForUser(string id, string userId)
    {
        var diagnosis = await GetById(id);
        if (diagnosis == null)
            return null;
        return diagnosis.UserId == userId ? diagnosis : null;
    }

    public async Task<AiDiagnosisModel?> GetById(string id)
    {
        var document = await _db.Collection("ai_diagnoses").Document(id).GetSnapshotAsync();
        return document.Exists ? document.ConvertTo<AiDiagnosisModel>() : null;
    }

    public async Task<string> Add(AiDiagnosisModel diagnosis)
    {
        var document = _db.Collection("ai_diagnoses").Document();
        diagnosis.DiagnosisId = document.Id;
        await document.SetAsync(diagnosis);
        return document.Id;
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("ai_diagnoses")
            .Document(id)
            .DeleteAsync();
    }

    public async Task MarkCareRecommendationsApplied(string id, Timestamp appliedAt)
    {
        await _db
            .Collection("ai_diagnoses")
            .Document(id)
            .UpdateAsync("careRecommendationsAppliedAt", appliedAt);
    }
}
