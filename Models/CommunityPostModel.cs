using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class CommunityPostModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("postId")]
    public string PostId { get; set; }

    [FirestoreProperty("authorId")]
    public string AuthorId { get; set; }

    [FirestoreProperty("authorName")]
    public string AuthorName { get; set; }

    [FirestoreProperty("authorAvatar")]
    public string AuthorAvatar { get; set; }

    [FirestoreProperty("content")]
    public string Content { get; set; }

    [FirestoreProperty("images")]
    public List<string> Images { get; set; } = new();

    [FirestoreProperty("likeCount")]
    public int LikeCount { get; set; }

    [FirestoreProperty("commentCount")]
    public int CommentCount { get; set; }

    [FirestoreProperty("status")]
    public string Status { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
