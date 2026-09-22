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

    public async Task UpdateRole(string id, string role, string adminUid, string adminEmail, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new SubscriptionDomainException("reason_required", "Vui lòng nhập lý do tối thiểu 5 ký tự.");
        var userRef = _db.Collection("users").Document(id);
        var snapshot = await userRef.GetSnapshotAsync();
        if (!snapshot.Exists) throw new SubscriptionDomainException("not_found", "Không tìm thấy người dùng.");
        var oldRole = snapshot.TryGetValue<string>("role", out var value) ? value : "user";
        var batch = _db.StartBatch();
        batch.Update(userRef, "role", role);
        batch.Set(_db.Collection("revenue_audit_logs").Document(), new { adminUid, adminEmail, action = "UpdateRole", targetType = "user", targetId = id, reason = reason.Trim(), before = $"{{\"role\":\"{oldRole}\"}}", after = $"{{\"role\":\"{role}\"}}", createdAt = Timestamp.GetCurrentTimestamp() });
        await batch.CommitAsync();
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
