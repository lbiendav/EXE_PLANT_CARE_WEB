using Google.Cloud.Firestore;

namespace HomePlant.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;

    public FirestoreService(FirestoreDb db)
    {
        _db = db;
    }

    public FirestoreDb Db => _db;
}