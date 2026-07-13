using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class UserService
{
    private readonly FirestoreDb _db;

    public UserService(FirestoreService firestore)
    {
        _db = firestore.Db;
    }

    public async Task Create(UserModel user)
    {
        user.CreatedAt = DateTime.UtcNow;

        await _db
            .Collection("users")
            .AddAsync(user);
    }

    public async Task<UserModel?> GetByEmail(string email)
    {
        var snapshot =
            await _db
                .Collection("users")
                .WhereEqualTo("email", email)
                .Limit(1)
                .GetSnapshotAsync();

        if (snapshot.Documents.Count == 0)
            return null;

        return snapshot.Documents[0]
            .ConvertTo<UserModel>();
    }

    public async Task<List<UserModel>> GetAll()
    {
        var snapshot =
            await _db
                .Collection("users")
                .GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<UserModel>())
            .ToList();
    }

    public async Task BanUser(string id)
    {
        await _db
            .Collection("users")
            .Document(id)
            .UpdateAsync("isLocked", true);
    }

    public async Task UnBanUser(string id)
    {
        await _db
            .Collection("users")
            .Document(id)
            .UpdateAsync("isLocked", false);
    }

    public async Task UpdateProfile(
        string uid,
        string fullName,
        string phone,
        string? avatarUrl)
    {
        var updates = new Dictionary<string, object>
        {
            { "displayName", fullName },
            { "phone", phone ?? "" }
        };

        if (!string.IsNullOrEmpty(avatarUrl))
            updates["avatarUrl"] = avatarUrl;

        await _db
            .Collection("users")
            .Document(uid)
            .UpdateAsync(updates);
    }
}