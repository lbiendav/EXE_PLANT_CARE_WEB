using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class UserModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("uid")]
    public string Uid { get; set; }

    [FirestoreProperty("displayName")]
    public string FullName { get; set; }

    [FirestoreProperty("email")]
    public string Email { get; set; }

    [FirestoreProperty("phone")]
    public string Phone { get; set; }

    [FirestoreProperty("avatarUrl")]
    public string AvatarUrl { get; set; }

    [FirestoreProperty("role")]
    public string Role { get; set; }

    [FirestoreProperty("isLocked")]
    public bool IsLocked { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}