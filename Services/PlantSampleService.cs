using Google.Cloud.Firestore;
using HomePlant.Models;
using System.Collections.Generic;

namespace HomePlant.Services;

public class PlantSampleService
{
    private readonly FirestoreDb _db;

    public PlantSampleService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task<List<PlantSampleModel>> GetAll()
    {
        var snapshot = await _db
            .Collection("sample_plants")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<PlantSampleModel>())
            .ToList();
    }

    public async Task Add(PlantSampleModel plant)
    {
        await _db
            .Collection("sample_plants")
            .AddAsync(plant);
    }

    public async Task Delete(string id)
    {
        await _db
            .Collection("sample_plants")
            .Document(id)
            .DeleteAsync();
    }

    public async Task Update(string id, PlantSampleModel plant)
    {
        await _db
            .Collection("sample_plants")
            .Document(id)
            .SetAsync(plant);
    }

    public async Task<List<PlantSampleModel>> GetTopPlants(int count = 4)
    {
        var snapshot = await _db
            .Collection("sample_plants")
            .Limit(count)
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<PlantSampleModel>())
            .ToList();
    }

    public async Task<PlantSampleModel?> GetById(string id)
    {
        var doc = await _db
            .Collection("sample_plants")
            .Document(id)
            .GetSnapshotAsync();

        if (!doc.Exists)
            return null;

        return doc.ConvertTo<PlantSampleModel>();
    }
}