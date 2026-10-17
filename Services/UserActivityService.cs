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

    public async Task LogDailyActivityAsync(
        string userId,
        string fullName,
        string email,
        string avatarUrl,
        bool isGuest,
        bool isNew,
        string path,
        string title,
        string action,
        bool isNewSession)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            var now = DateTime.UtcNow;
            var vnNow = now.AddHours(7);
            var dateKey = vnNow.ToString("yyyyMMdd");
            var docId = $"{dateKey}_{userId}";

            var activityItem = new Dictionary<string, object>
            {
                { "path", path },
                { "title", title },
                { "action", action },
                { "time", vnNow.ToString("HH:mm:ss") },
                { "timestamp", Timestamp.FromDateTime(now) }
            };

            var docRef = _db.Collection("daily_user_activities").Document(docId);
            var snap = await docRef.GetSnapshotAsync();

            if (!snap.Exists)
            {
                var data = new Dictionary<string, object>
                {
                    { "dateKey", dateKey },
                    { "uid", userId },
                    { "fullName", fullName },
                    { "email", email },
                    { "avatarUrl", avatarUrl },
                    { "isNew", isNew },
                    { "isGuest", isGuest },
                    { "sessionCount", 1 },
                    { "pageViewCount", 1 },
                    { "lastActiveAt", Timestamp.FromDateTime(now) },
                    { "lastActiveTime", vnNow.ToString("HH:mm:ss") },
                    { "activities", new List<object> { activityItem } }
                };
                await docRef.SetAsync(data);
            }
            else
            {
                var updates = new Dictionary<string, object>
                {
                    { "lastActiveAt", Timestamp.FromDateTime(now) },
                    { "lastActiveTime", vnNow.ToString("HH:mm:ss") },
                    { "pageViewCount", FieldValue.Increment(1) },
                    { "activities", FieldValue.ArrayUnion(activityItem) }
                };

                if (isNewSession)
                {
                    updates["sessionCount"] = FieldValue.Increment(1);
                }

                if (!isGuest && snap.ContainsField("isGuest") && snap.GetValue<bool>("isGuest"))
                {
                    updates["fullName"] = fullName;
                    updates["email"] = email;
                    updates["avatarUrl"] = avatarUrl;
                    updates["isGuest"] = false;
                    updates["isNew"] = isNew;
                }

                await docRef.UpdateAsync(updates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log daily activity for {UserId}", userId);
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
                .Where(a => !a.Path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) &&
                            !a.Path.StartsWith("/Revenue", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch activities for user {Uid}", uid);
            return [];
        }
    }

    public async Task<List<DailyUserSummaryVM>> GetDailyUsersAsync(string dateKey, string type = "all")
    {
        var result = new List<DailyUserSummaryVM>();
        if (string.IsNullOrWhiteSpace(dateKey)) return result;

        try
        {
            var snapshot = await _db.Collection("daily_user_activities")
                .WhereEqualTo("dateKey", dateKey)
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var isNew = doc.ContainsField("isNew") && doc.GetValue<bool>("isNew");
                var isGuest = doc.ContainsField("isGuest") && doc.GetValue<bool>("isGuest");

                var activitiesList = new List<UserActivityItemVM>();
                if (doc.ContainsField("activities"))
                {
                    var rawList = doc.GetValue<List<object>>("activities");
                    if (rawList != null)
                    {
                        foreach (var item in rawList)
                        {
                            if (item is Dictionary<string, object> dict)
                            {
                                activitiesList.Add(new UserActivityItemVM
                                {
                                    Path = dict.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "",
                                    Title = dict.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "",
                                    Action = dict.TryGetValue("action", out var a) ? a?.ToString() ?? "Xem trang" : "Xem trang",
                                    Time = dict.TryGetValue("time", out var tm) ? tm?.ToString() ?? "" : ""
                                });
                            }
                        }
                    }
                }

                result.Add(new DailyUserSummaryVM
                {
                    Uid = doc.ContainsField("uid") ? doc.GetValue<string>("uid") : doc.Id,
                    FullName = doc.ContainsField("fullName") ? doc.GetValue<string>("fullName") : "Người dùng",
                    Email = doc.ContainsField("email") ? doc.GetValue<string>("email") : "",
                    AvatarUrl = doc.ContainsField("avatarUrl") ? doc.GetValue<string>("avatarUrl") : "",
                    IsNew = isNew,
                    IsGuest = isGuest,
                    SessionCount = doc.ContainsField("sessionCount") ? Convert.ToInt32(doc.GetValue<object>("sessionCount")) : 1,
                    PageViewCount = doc.ContainsField("pageViewCount") ? Convert.ToInt32(doc.GetValue<object>("pageViewCount")) : activitiesList.Count,
                    LastActiveTime = doc.ContainsField("lastActiveTime") ? doc.GetValue<string>("lastActiveTime") : "",
                    Activities = activitiesList
                });
            }

            // Fallback: If daily_user_activities has no records yet for this date, query users collection
            if (result.Count == 0)
            {
                var allUsers = await _db.Collection("users").GetSnapshotAsync();
                foreach (var doc in allUsers.Documents)
                {
                    var u = doc.ConvertTo<UserModel>();
                    var createdVn = u.CreatedAt.AddHours(7).ToString("yyyyMMdd");
                    var lastActiveVn = u.LastActiveAt?.AddHours(7).ToString("yyyyMMdd");

                    var isUserNew = createdVn == dateKey;
                    var isActiveToday = lastActiveVn == dateKey || isUserNew;

                    if (isActiveToday)
                    {
                        var userActivities = await GetUserActivitiesAsync(u.Id, 25);
                        var dayActivities = userActivities
                            .Where(a => a.CreatedAt.AddHours(7).ToString("yyyyMMdd") == dateKey)
                            .Select(a => new UserActivityItemVM
                            {
                                Path = a.Path,
                                Title = a.Title,
                                Action = a.Action,
                                Time = a.FormattedTime
                            })
                            .ToList();

                        result.Add(new DailyUserSummaryVM
                        {
                            Uid = u.Id,
                            FullName = string.IsNullOrWhiteSpace(u.FullName) ? "Người dùng" : u.FullName,
                            Email = u.Email,
                            AvatarUrl = u.AvatarUrl,
                            IsNew = isUserNew || u.VisitCount <= 1,
                            IsGuest = false,
                            SessionCount = Math.Max(1, u.VisitCount),
                            PageViewCount = Math.Max(1, dayActivities.Count),
                            LastActiveTime = u.LastActiveAt?.AddHours(7).ToString("HH:mm:ss") ?? "",
                            Activities = dayActivities
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get daily users for date {DateKey}", dateKey);
        }

        if (string.Equals(type, "new", StringComparison.OrdinalIgnoreCase))
        {
            return result.Where(u => u.IsNew).ToList();
        }
        if (string.Equals(type, "returning", StringComparison.OrdinalIgnoreCase))
        {
            return result.Where(u => !u.IsNew).ToList();
        }

        return result;
    }
}
