using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class CareLogService
{
    private readonly FirestoreDb _db;

    public CareLogService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<CareLogModel>> GetLogs(
        string plantId)
    {
        var snapshot = await _db
            .Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .OrderByDescending("CreatedAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<CareLogModel>())
            .ToList();
    }

    public async Task AddLog(
        string plantId,
        CareLogModel log)
    {
        await _db
            .Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .AddAsync(log);
    }

    public async Task DeleteLog(
        string plantId,
        string logId)
    {
        await _db
            .Collection("plants")
            .Document(plantId)
            .Collection("careLogs")
            .Document(logId)
            .DeleteAsync();
    }
}