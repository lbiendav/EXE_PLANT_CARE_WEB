using Google.Cloud.Firestore;
using HomePlant.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HomePlant.Services;

public class ArticleService
{
    private readonly FirestoreDb _db;
    private readonly IMemoryCache? _cache;
    private const string CacheKey = "HomePlant:Articles:All";

    public ArticleService(FirestoreService firestore, IMemoryCache? cache = null)
    {
        _db = firestore.Db;
        _cache = cache;
    }

    public async Task<List<ArticleModel>> GetAll()
    {
        if (_cache != null && _cache.TryGetValue<List<ArticleModel>>(CacheKey, out var cached) && cached != null)
            return cached;

        var snapshot = await _db
            .Collection("articles")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var list = snapshot.Documents
            .Select(x => x.ConvertTo<ArticleModel>())
            .ToList();

        _cache?.Set(CacheKey, list, TimeSpan.FromMinutes(10));
        return list;
    }

    public async Task<int> Count() => checked((int)((await _db.Collection("articles").Count().GetSnapshotAsync()).Count ?? 0));

    public async Task<ArticleModel?> GetById(string id)
    {
        if (!FirestoreDocumentId.IsValid(id))
            return null;

        var doc = await _db
            .Collection("articles")
            .Document(id)
            .GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<ArticleModel>();
    }

    public async Task<ArticleModel?> GetByIdAndIncrementViews(string id)
    {
        if (!FirestoreDocumentId.IsValid(id))
            return null;

        var reference = _db.Collection("articles").Document(id);
        var doc = await reference.GetSnapshotAsync();
        if (!doc.Exists) return null;
        await reference.UpdateAsync("views", FieldValue.Increment(1));
        var article = doc.ConvertTo<ArticleModel>();
        article.Views++;
        return article;
    }

    public async Task Add(ArticleModel article)
    {
        _cache?.Remove(CacheKey);
        await _db.Collection("articles")
            .AddAsync(article);
    }

    public async Task Update(
        string id,
        ArticleModel article)
    {
        _cache?.Remove(CacheKey);
        await _db
            .Collection("articles")
            .Document(id)
            .SetAsync(article);
    }

    public async Task Delete(string id)
    {
        _cache?.Remove(CacheKey);
        await _db
            .Collection("articles")
            .Document(id)
            .DeleteAsync();
    }

    public async Task<List<ArticleModel>> GetLatest(int count = 3)
    {
        var all = await GetAll();
        return all.Take(count).ToList();
    }
}
