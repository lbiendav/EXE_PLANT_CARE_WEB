using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class FeedbackModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("userName")]
    public string UserName { get; set; } = "";

    [FirestoreProperty("userSubtitle")]
    public string UserSubtitle { get; set; } = "";

    [FirestoreProperty("userAvatar")]
    public string UserAvatar { get; set; } = "";

    [FirestoreProperty("tag")]
    public string Tag { get; set; } = "CỨU CÂY THÀNH CÔNG";

    [FirestoreProperty("tagIcon")]
    public string TagIcon { get; set; } = "🌿";

    [FirestoreProperty("tagType")]
    public string TagType { get; set; } = "success"; // success, info, gold, warning

    [FirestoreProperty("rating")]
    public int Rating { get; set; } = 5;

    [FirestoreProperty("title")]
    public string Title { get; set; } = "";

    [FirestoreProperty("content")]
    public string Content { get; set; } = "";

    [FirestoreProperty("isFeatured")]
    public bool IsFeatured { get; set; } = true;

    [FirestoreProperty("status")]
    public string Status { get; set; } = "pending";

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; } = Timestamp.GetCurrentTimestamp();
}
