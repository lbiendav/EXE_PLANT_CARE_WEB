using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.ViewModels;

namespace HomePlant.Services;

public class UserActivityService
{
    private readonly FirestoreDb _db;
    private readonly GoogleAnalyticsService _gaService;
    private readonly ILogger<UserActivityService> _logger;

    public UserActivityService(
        FirestoreService firestore,
        GoogleAnalyticsService gaService,
        ILogger<UserActivityService> logger)
    {
        _db = firestore.Db;
        _gaService = gaService;
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
        if (isGuest || string.IsNullOrWhiteSpace(userId)) return;

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

            var addedUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var doc in snapshot.Documents)
            {
                var isGuest = doc.ContainsField("isGuest") && doc.GetValue<bool>("isGuest");
                if (isGuest) continue;

                var uid = doc.ContainsField("uid") ? doc.GetValue<string>("uid") : doc.Id;
                if (string.IsNullOrWhiteSpace(uid) || uid.StartsWith("ga4_", StringComparison.OrdinalIgnoreCase) || uid.StartsWith("guest_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isNew = doc.ContainsField("isNew") && doc.GetValue<bool>("isNew");

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
                    Uid = uid,
                    FullName = doc.ContainsField("fullName") ? doc.GetValue<string>("fullName") : "Người dùng",
                    Email = doc.ContainsField("email") ? doc.GetValue<string>("email") : "",
                    AvatarUrl = doc.ContainsField("avatarUrl") ? doc.GetValue<string>("avatarUrl") : "",
                    IsNew = isNew,
                    IsGuest = false,
                    SessionCount = doc.ContainsField("sessionCount") ? Convert.ToInt32(doc.GetValue<object>("sessionCount")) : 1,
                    PageViewCount = doc.ContainsField("pageViewCount") ? Convert.ToInt32(doc.GetValue<object>("pageViewCount")) : activitiesList.Count,
                    LastActiveTime = doc.ContainsField("lastActiveTime") ? doc.GetValue<string>("lastActiveTime") : "",
                    Activities = activitiesList
                });
                addedUids.Add(uid);
            }

            // Also check users created on this day (to ensure any newly registered user is included)
            if (DateTime.TryParseExact(dateKey, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dayDt))
            {
                var dayStartUtc = DateTime.SpecifyKind(dayDt.AddHours(-7), DateTimeKind.Utc);
                var dayEndUtc = DateTime.SpecifyKind(dayDt.AddDays(1).AddHours(-7), DateTimeKind.Utc);

                var startTs = Timestamp.FromDateTime(dayStartUtc);
                var endTs = Timestamp.FromDateTime(dayEndUtc);

                var newUsersSnap = await _db.Collection("users")
                    .WhereGreaterThanOrEqualTo("createdAt", startTs)
                    .WhereLessThan("createdAt", endTs)
                    .Limit(50)
                    .GetSnapshotAsync();

                foreach (var doc in newUsersSnap.Documents)
                {
                    var u = doc.ConvertTo<UserModel>();
                    if (u != null && !string.IsNullOrWhiteSpace(u.Id) && !addedUids.Contains(u.Id))
                    {
                        result.Add(new DailyUserSummaryVM
                        {
                            Uid = u.Id,
                            FullName = string.IsNullOrWhiteSpace(u.FullName) ? "Người dùng" : u.FullName,
                            Email = u.Email,
                            AvatarUrl = u.AvatarUrl,
                            IsNew = true,
                            IsGuest = false,
                            SessionCount = Math.Max(1, u.VisitCount),
                            PageViewCount = 1,
                            LastActiveTime = u.CreatedAt.AddHours(7).ToString("HH:mm:ss"),
                            Activities =
                            [
                                new UserActivityItemVM
                                {
                                    Path = "/Account/Register",
                                    Title = "Đăng ký tài khoản HomePlant",
                                    Action = "Tạo tài khoản mới",
                                    Time = u.CreatedAt.AddHours(7).ToString("HH:mm:ss")
                                }
                            ]
                        });
                        addedUids.Add(u.Id);
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

    public async Task<GoogleAnalyticsOverviewVM> EnrichWithDatabaseUsersAsync(GoogleAnalyticsOverviewVM gaOverview)
    {
        try
        {
            var minDateKey = DateTime.UtcNow.AddHours(7).AddDays(-30).ToString("yyyyMMdd");
            var dailySnap = await _db.Collection("daily_user_activities")
                .WhereGreaterThanOrEqualTo("dateKey", minDateKey)
                .GetSnapshotAsync();

            var dailyDocsByDate = dailySnap.Documents
                .Where(d => {
                    var isGuest = d.ContainsField("isGuest") && d.GetValue<bool>("isGuest");
                    var uid = d.ContainsField("uid") ? d.GetValue<string>("uid") : d.Id;
                    return !isGuest && !string.IsNullOrWhiteSpace(uid) && !uid.StartsWith("ga4_", StringComparison.OrdinalIgnoreCase) && !uid.StartsWith("guest_", StringComparison.OrdinalIgnoreCase);
                })
                .GroupBy(d => d.ContainsField("dateKey") ? d.GetValue<string>("dateKey") : "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Also query all users created in the last 30 days to ensure any registered user on each date is accounted for
            var minDateUtc = DateTime.UtcNow.AddHours(7).Date.AddDays(-30).AddHours(-7);
            var usersCreatedSnap = await _db.Collection("users")
                .WhereGreaterThanOrEqualTo("createdAt", Timestamp.FromDateTime(DateTime.SpecifyKind(minDateUtc, DateTimeKind.Utc)))
                .GetSnapshotAsync();

            var newUsersByDate = usersCreatedSnap.Documents
                .Select(d => d.ConvertTo<UserModel>())
                .Where(u => u != null && !string.IsNullOrWhiteSpace(u.Id))
                .GroupBy(u => u.CreatedAt.AddHours(7).ToString("yyyyMMdd"))
                .ToDictionary(g => g.Key, g => g.ToList());

            var updatedDailyTrends = new List<AnalyticsDailyRowVM>();
            long totalDatabaseNewUsers = 0;
            long totalDatabaseReturningUsers = 0;
            var distinctActiveUserIdsIn30Days = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var existingDailyRows = gaOverview.DailyTrends.ToDictionary(d => d.DateKey, d => d);

            // Generate full 30 consecutive days up to today (descending: newest first)
            var todayVn = DateTime.UtcNow.AddHours(7).Date;
            var dateKeysToProcess = Enumerable.Range(0, 30)
                .Select(i => todayVn.AddDays(-i).ToString("yyyyMMdd"))
                .ToList();

            foreach (var dateKey in dateKeysToProcess)
            {
                var displayDate = dateKey.Length == 8
                    ? $"{dateKey[6..8]}/{dateKey[4..6]}"
                    : dateKey;

                var dayActiveUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var dayNewUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var dayReturningUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Process daily activity records for real members
                if (dailyDocsByDate.TryGetValue(dateKey, out var dayDocs) && dayDocs.Count > 0)
                {
                    foreach (var doc in dayDocs)
                    {
                        var uid = doc.ContainsField("uid") ? doc.GetValue<string>("uid") : doc.Id;
                        var isNew = doc.ContainsField("isNew") && doc.GetValue<bool>("isNew");
                        dayActiveUids.Add(uid);
                        distinctActiveUserIdsIn30Days.Add(uid);
                        if (isNew) dayNewUids.Add(uid);
                        else dayReturningUids.Add(uid);
                    }
                }

                // 2. Add any users who registered on that date
                if (newUsersByDate.TryGetValue(dateKey, out var newUsersList))
                {
                    foreach (var u in newUsersList)
                    {
                        dayActiveUids.Add(u.Id);
                        dayNewUids.Add(u.Id);
                        distinctActiveUserIdsIn30Days.Add(u.Id);
                        dayReturningUids.Remove(u.Id);
                    }
                }

                existingDailyRows.TryGetValue(dateKey, out var originalRow);
                var sessions = originalRow?.Sessions ?? 0;
                var pageViews = originalRow?.PageViews ?? 0;
                var display = originalRow?.DisplayDate ?? displayDate;

                // Member counts strictly from real database accounts (excluding all anonymous guests):
                long newCount = dayNewUids.Count;
                long returningCount = dayReturningUids.Count;
                long activeCount = dayActiveUids.Count;

                totalDatabaseNewUsers += newCount;
                totalDatabaseReturningUsers += returningCount;

                updatedDailyTrends.Add(new AnalyticsDailyRowVM(
                    dateKey,
                    display,
                    activeCount,
                    sessions,
                    pageViews,
                    newCount,
                    returningCount
                ));
            }

            var daysCount = Math.Max(1, updatedDailyTrends.Count);
            double avgDailyUsers = (double)updatedDailyTrends.Sum(d => d.ActiveUsers) / daysCount;
            double avgDailySessions = (double)updatedDailyTrends.Sum(d => d.Sessions) / daysCount;
            double avgDailyPageViews = (double)updatedDailyTrends.Sum(d => d.PageViews) / daysCount;

            return new GoogleAnalyticsOverviewVM
            {
                Configured = gaOverview.Configured,
                Available = gaOverview.Available || updatedDailyTrends.Count > 0,
                Error = gaOverview.Error,
                ActiveUsers30Days = distinctActiveUserIdsIn30Days.Count > 0 ? distinctActiveUserIdsIn30Days.Count : gaOverview.ActiveUsers30Days,
                Sessions30Days = gaOverview.Sessions30Days,
                PageViews30Days = gaOverview.PageViews30Days,
                NewUsers30Days = totalDatabaseNewUsers,
                ReturningUsers30Days = totalDatabaseReturningUsers,
                AverageDailyUsers30Days = avgDailyUsers,
                AverageDailySessions30Days = avgDailySessions,
                AverageDailyPageViews30Days = avgDailyPageViews,
                AverageEngagementSeconds = gaOverview.AverageEngagementSeconds,
                BounceRate = gaOverview.BounceRate,
                TrafficSources = gaOverview.TrafficSources,
                PopularPages = gaOverview.PopularPages,
                LeastVisitedPages = gaOverview.LeastVisitedPages,
                DailyTrends = updatedDailyTrends,
                DeviceBreakdown = gaOverview.DeviceBreakdown,
                LandingPages = gaOverview.LandingPages,
                Countries = gaOverview.Countries
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich overview with database users");
            return gaOverview;
        }
    }
}
