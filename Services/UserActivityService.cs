using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class UserActivityService
{
    private readonly FirestoreDb _db;
    private readonly ILogger<UserActivityService> _logger;

    public UserActivityService(FirestoreService firestore, ILogger<UserActivityService> logger)
    {
        _db = firestore.Db;
        _logger = logger;
    }

    public async Task LogActivityAsync(string uid, string path, string title, string action, bool isNewSession)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;

        try
        {
            var userRef = _db.Collection("users").Document(uid);
            var now = DateTime.UtcNow;

            var userUpdates = new Dictionary<string, object>
            {
                { "lastActiveAt", Timestamp.FromDateTime(now) }
            };

            if (isNewSession)
            {
                userUpdates["visitCount"] = FieldValue.Increment(1);
            }

            await userRef.SetAsync(userUpdates, SetOptions.MergeAll);

            var activityDoc = new Dictionary<string, object>
            {
                { "path", path },
                { "title", title },
                { "action", action },
                { "createdAt", Timestamp.FromDateTime(now) }
            };

            await userRef.Collection("activities").AddAsync(activityDoc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log activity for user {Uid}", uid);
        }
    }

    public async Task<List<UserActivityModel>> GetUserActivitiesAsync(string uid, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(uid)) return [];

        try
        {
            var snapshot = await _db.Collection("users")
                .Document(uid)
                .Collection("activities")
                .OrderByDescending("createdAt")
                .Limit(limit)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(doc => doc.ConvertTo<UserActivityModel>())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch activities for user {Uid}", uid);
            return [];
        }
    }
}
