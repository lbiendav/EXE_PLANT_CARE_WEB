using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class CommunityPostService
{
    private readonly FirestoreDb _db;

    public CommunityPostService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<CommunityPostModel>> GetAll()
    {
        var snapshot = await _db
            .Collection("community_posts")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<CommunityPostModel>())
            .ToList();
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("community_posts")
            .Document(id)
            .DeleteAsync();
    }
}
