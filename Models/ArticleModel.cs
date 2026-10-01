using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace HomePlant.Models;

[FirestoreData]
public class ArticleModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("title")]
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = "";

    [FirestoreProperty("content")]
    [Required, StringLength(50_000, MinimumLength = 10)]
    public string Content { get; set; } = "";

    [FirestoreProperty("coverImage")]
    [StringLength(2048)]
    public string? CoverImage { get; set; }

    [FirestoreProperty("tags")]
    public List<string> Tags { get; set; } = new();

    [FirestoreProperty("views")]
    public int Views { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
