using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class CareLogModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty]
    public string UserId { get; set; }

    [FirestoreProperty]
    public string ActionType { get; set; }

    [FirestoreProperty]
    public string Note { get; set; }

    [FirestoreProperty]
    public string ImageUrl { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}