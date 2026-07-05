using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class QaThreadService
{
    private readonly FirestoreDb _db;

    public QaThreadService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<QaThreadModel>> GetAll()
    {
        var snapshot = await _db
            .Collection("qa_threads")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<QaThreadModel>())
            .ToList();
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("qa_threads")
            .Document(id)
            .DeleteAsync();
    }
}
