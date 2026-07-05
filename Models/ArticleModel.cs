using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class ArticleModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("title")]
    public string Title { get; set; }

    [FirestoreProperty("content")]
    public string Content { get; set; }

    [FirestoreProperty("coverImage")]
    public string CoverImage { get; set; }

    [FirestoreProperty("tags")]
    public List<string> Tags { get; set; } = new();

    [FirestoreProperty("views")]
    public int Views { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
