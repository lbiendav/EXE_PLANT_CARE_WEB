using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class QaThreadModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("threadId")]
    public string ThreadId { get; set; }

    [FirestoreProperty("userId")]
    public string UserId { get; set; }

    [FirestoreProperty("userName")]
    public string UserName { get; set; }

    [FirestoreProperty("userAvatar")]
    public string UserAvatar { get; set; }

    [FirestoreProperty("expertId")]
    public string ExpertId { get; set; }

    [FirestoreProperty("expertName")]
    public string ExpertName { get; set; }

    [FirestoreProperty("title")]
    public string Title { get; set; }

    [FirestoreProperty("lastMessage")]
    public string LastMessage { get; set; }

    [FirestoreProperty("lastMessageAt")]
    public Timestamp LastMessageAt { get; set; }

    [FirestoreProperty("status")]
    public string Status { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
