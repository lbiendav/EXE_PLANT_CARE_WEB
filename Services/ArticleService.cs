using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class ArticleService
{
    private readonly FirestoreDb _db;

    public ArticleService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<ArticleModel>> GetAll()
    {
        var snapshot = await _db
            .Collection("articles")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<ArticleModel>())
            .ToList();
    }

    public async Task<ArticleModel?> GetById(string id)
    {
        var doc = await _db
            .Collection("articles")
            .Document(id)
            .GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<ArticleModel>();
    }

    public async Task Add(ArticleModel article)
    {
        await _db.Collection("articles")
            .AddAsync(article);
    }

    public async Task Update(
        string id,
        ArticleModel article)
    {
        await _db
            .Collection("articles")
            .Document(id)
            .SetAsync(article);
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("articles")
            .Document(id)
            .DeleteAsync();
    }

    public async Task<List<ArticleModel>> GetLatest(int count = 3)
    {
        var snapshot = await _db
        .Collection("articles")
        .OrderByDescending("createdAt")
        .Limit(count)
        .GetSnapshotAsync();
        return snapshot.Documents
            .Select(x => x.ConvertTo<ArticleModel>())
            .ToList();
    }
}