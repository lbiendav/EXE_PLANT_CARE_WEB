using Google.Cloud.Firestore;
using HomePlant.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;

namespace HomePlant.Services;

public class PlantSampleService
{
    private readonly FirestoreDb _db;
    private readonly IMemoryCache? _cache;
    private const string CacheKey = "HomePlant:PlantSamples:All";

    public PlantSampleService(FirestoreService firestore, IMemoryCache? cache = null)
    {
        _db = firestore.Db;
        _cache = cache;
    }

    public async Task<List<PlantSampleModel>> GetAll()
    {
        if (_cache != null && _cache.TryGetValue<List<PlantSampleModel>>(CacheKey, out var cached) && cached != null)
            return cached;

        var snapshot = await _db
            .Collection("sample_plants")
            .GetSnapshotAsync();

        var list = snapshot.Documents
            .Select(x => x.ConvertTo<PlantSampleModel>())
            .ToList();

        _cache?.Set(CacheKey, list, TimeSpan.FromMinutes(15));
        return list;
    }

    public async Task<int> Count() => checked((int)((await _db.Collection("sample_plants").Count().GetSnapshotAsync()).Count ?? 0));

    public async Task Add(PlantSampleModel plant)
    {
        _cache?.Remove(CacheKey);
        await _db
            .Collection("sample_plants")
            .AddAsync(plant);
    }

    public async Task Delete(string id)
    {
        _cache?.Remove(CacheKey);
        await _db
            .Collection("sample_plants")
            .Document(id)
            .DeleteAsync();
    }

    public async Task Update(string id, PlantSampleModel plant)
    {
        _cache?.Remove(CacheKey);
        await _db
            .Collection("sample_plants")
            .Document(id)
            .SetAsync(plant);
    }

    public async Task<List<PlantSampleModel>> GetTopPlants(int count = 4)
    {
        var all = await GetAll();
        return all.Take(count).ToList();
    }

    public async Task<PlantSampleModel?> GetById(string id)
    {
        if (!FirestoreDocumentId.IsValid(id))
            return null;

        var doc = await _db
            .Collection("sample_plants")
            .Document(id)
            .GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<PlantSampleModel>();
    }
}
