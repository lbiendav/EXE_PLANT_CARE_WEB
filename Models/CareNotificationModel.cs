using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class CareNotificationModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = "";

    [FirestoreProperty("plantId")]
    public string PlantId { get; set; } = "";

    [FirestoreProperty("plantName")]
    public string PlantName { get; set; } = "";

    [FirestoreProperty("careType")]
    public string CareType { get; set; } = "";

    [FirestoreProperty("title")]
    public string Title { get; set; } = "";

    [FirestoreProperty("message")]
    public string Message { get; set; } = "";

    [FirestoreProperty("dueAt")]
    public Timestamp DueAt { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("readAt")]
    public Timestamp? ReadAt { get; set; }

    [FirestoreProperty("isRead")]
    public bool IsRead { get; set; }

    [FirestoreProperty("emailSentAt")]
    public Timestamp? EmailSentAt { get; set; }

    [FirestoreProperty("emailClaimedAt")]
    public Timestamp? EmailClaimedAt { get; set; }

}
