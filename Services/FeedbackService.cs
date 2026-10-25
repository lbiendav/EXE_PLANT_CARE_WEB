using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public class FeedbackService
{
    private readonly FirestoreService _firestoreService;
    private readonly ILogger<FeedbackService> _logger;
    private static readonly List<FeedbackModel> _memoryStore = new();
    private static readonly object _lock = new();

    public FeedbackService(FirestoreService firestoreService, ILogger<FeedbackService> logger)
    {
        _firestoreService = firestoreService;
        _logger = logger;
    }

    /// <summary>
    /// Gets approved feedbacks for the Home page showcase.
    /// Strictly excludes any pending or hidden submissions.
    /// </summary>
    public async Task<List<FeedbackModel>> GetFeaturedFeedbacksAsync()
    {
        var result = new List<FeedbackModel>();

        try
        {
            var collection = _firestoreService.Db.Collection("feedbacks");
            var snapshot = await collection
                .WhereEqualTo("status", "approved")
                .GetSnapshotAsync();

            var list = snapshot.Documents
                .Select(d =>
                {
                    try
                    {
                        var m = d.ConvertTo<FeedbackModel>();
                        m.Id = d.Id;
                        return m;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x != null && x.Status == "approved")
                .Cast<FeedbackModel>()
                .OrderByDescending(f => f.CreatedAt.ToDateTime())
                .ToList();

            if (list.Count > 0)
            {
                result.AddRange(list);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve approved feedbacks from Firestore; using memory store and defaults.");
        }

        // Include any approved memory items not in Firestore
        lock (_lock)
        {
            foreach (var item in _memoryStore.Where(m => m.Status == "approved"))
            {
                if (!result.Any(r => r.Id == item.Id))
                {
                    result.Insert(0, item);
                }
            }
        }

        // Filter out any test submissions
        result = result
            .Where(f => !f.UserName.Contains("Chi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Always ensure the 3 canonical featured cards exist
        var defaults = GetDefaultFeedbacks();
        foreach (var def in defaults)
        {
            if (!result.Any(r => r.Title == def.Title || r.Id == def.Id))
            {
                result.Add(def);
            }
        }

        // Strictly return only approved feedbacks (maximum 3 for the home page showcase)
        return result.Take(3).ToList();
    }

    /// <summary>
    /// Gets all feedbacks for the Admin moderation dashboard.
    /// Supports filtering by status: "pending", "approved", "hidden", or "all".
    /// </summary>
    public async Task<List<FeedbackModel>> GetAllFeedbacksAsync(string? statusFilter = null)
    {
        var result = new List<FeedbackModel>();

        try
        {
            var collection = _firestoreService.Db.Collection("feedbacks");
            Query query = collection;

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
            {
                query = query.WhereEqualTo("status", statusFilter);
            }

            var snapshot = await query.GetSnapshotAsync();

            var list = snapshot.Documents
                .Select(d =>
                {
                    try
                    {
                        var m = d.ConvertTo<FeedbackModel>();
                        m.Id = d.Id;
                        return m;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x != null)
                .Cast<FeedbackModel>()
                .OrderByDescending(f => f.CreatedAt.ToDateTime())
                .ToList();

            result.AddRange(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch all feedbacks from Firestore.");
        }

        // Merge from memory store
        lock (_lock)
        {
            foreach (var item in _memoryStore)
            {
                if (!result.Any(r => r.Id == item.Id))
                {
                    if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "all" || item.Status == statusFilter)
                    {
                        result.Insert(0, item);
                    }
                }
            }
        }

        // Include defaults if viewing approved or all
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter is "all" or "approved")
        {
            foreach (var def in GetDefaultFeedbacks())
            {
                if (!result.Any(r => r.Id == def.Id || r.Title == def.Title))
                {
                    result.Add(def);
                }
            }
        }

        return result.OrderByDescending(f => f.CreatedAt.ToDateTime()).ToList();
    }

    /// <summary>
    /// Creates a new user feedback with status "pending" (requires admin approval).
    /// </summary>
    public async Task<FeedbackModel> CreateFeedbackAsync(FeedbackModel feedback)
    {
        feedback.CreatedAt = Timestamp.GetCurrentTimestamp();
        feedback.Status = "pending"; // Must be pending until approved by admin!
        feedback.IsFeatured = false;

        if (string.IsNullOrWhiteSpace(feedback.Id))
        {
            feedback.Id = Guid.NewGuid().ToString("N");
        }

        try
        {
            var collection = _firestoreService.Db.Collection("feedbacks");
            var docRef = await collection.AddAsync(feedback);
            feedback.Id = docRef.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firestore unavailable for feedback creation. Saved in memory store.");
        }

        lock (_lock)
        {
            _memoryStore.Insert(0, feedback);
            if (_memoryStore.Count > 100)
            {
                _memoryStore.RemoveAt(_memoryStore.Count - 1);
            }
        }

        return feedback;
    }

    /// <summary>
    /// Updates feedback status (e.g. "approved", "pending", "hidden").
    /// </summary>
    public async Task UpdateStatusAsync(string id, string status)
    {
        try
        {
            var docRef = _firestoreService.Db.Collection("feedbacks").Document(id);
            await docRef.UpdateAsync("status", status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update feedback status in Firestore for ID {Id}", id);
        }

        lock (_lock)
        {
            var item = _memoryStore.FirstOrDefault(m => m.Id == id);
            if (item != null)
            {
                item.Status = status;
            }
        }
    }

    /// <summary>
    /// Deletes a feedback document.
    /// </summary>
    public async Task DeleteFeedbackAsync(string id)
    {
        try
        {
            var docRef = _firestoreService.Db.Collection("feedbacks").Document(id);
            await docRef.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete feedback from Firestore for ID {Id}", id);
        }

        lock (_lock)
        {
            _memoryStore.RemoveAll(m => m.Id == id);
        }
    }

    /// <summary>
    /// Counts pending feedbacks awaiting admin approval.
    /// </summary>
    public async Task<int> CountPendingAsync()
    {
        try
        {
            var collection = _firestoreService.Db.Collection("feedbacks");
            var snapshot = await collection
                .WhereEqualTo("status", "pending")
                .GetSnapshotAsync();

            int count = snapshot.Count;

            lock (_lock)
            {
                foreach (var item in _memoryStore.Where(m => m.Status == "pending"))
                {
                    if (!snapshot.Documents.Any(d => d.Id == item.Id))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count pending feedbacks in Firestore.");
            lock (_lock)
            {
                return _memoryStore.Count(m => m.Status == "pending");
            }
        }
    }

    public static List<FeedbackModel> GetDefaultFeedbacks()
    {
        return new List<FeedbackModel>
        {
            new FeedbackModel
            {
                Id = "seed-1",
                UserName = "Nguyễn Văn Nam",
                UserSubtitle = "17/10/2026 · Vườn 12 cây",
                Tag = "CỨU CÂY THÀNH CÔNG",
                TagIcon = "🌿",
                TagType = "success",
                Rating = 5,
                Title = "Hồi sinh cây Monstera sau 1 tuần",
                Content = "Nhờ tính năng chẩn đoán AI của HomePlant, mình biết ngay cây bị úng rễ do giá thể bí khí. Làm theo đúng lộ trình cứu cây và đổi đất thoát nước, chỉ sau 10 ngày mầm lá mới đã bung xòe mơn mởn!",
                IsFeatured = true,
                Status = "approved",
                CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc))
            },
            new FeedbackModel
            {
                Id = "seed-2",
                UserName = "Lê Thị Thu Hương",
                UserSubtitle = "10/10/2026 · Căn hộ chung cư",
                Tag = "NGƯỜI MỚI BẮT ĐẦU",
                TagIcon = "🌱",
                TagType = "info",
                Rating = 5,
                Title = "Không còn sợ 'trồng cây nào chết cây đó'",
                Content = "Trước đây mình toàn tưới theo cảm tính làm cây thối rễ liên tục. Từ ngày cài lịch nhắc việc theo mùa và nhận thông báo của HomePlant, cả 6 chậu cây phòng khách đều xanh tốt. Giao diện cực kỳ thư thái và dễ dùng.",
                IsFeatured = true,
                Status = "approved",
                CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 10, 10, 14, 30, 0, DateTimeKind.Utc))
            },
            new FeedbackModel
            {
                Id = "seed-3",
                UserName = "Trần Minh Hoàng",
                UserSubtitle = "02/10/2026 · Ban công xanh",
                Tag = "HỘI VIÊN GOLD",
                TagIcon = "✨",
                TagType = "gold",
                Rating = 5,
                Title = "Chuyên gia thực vật hướng dẫn tận tình",
                Content = "Chậu bàng Singapore cao 1m8 của mình bị rệp sáp diện rộng tưởng phải bỏ. Nhờ chuyên gia HomePlant tư vấn phác đồ sinh học an toàn cho nhà có trẻ nhỏ, cây đã sạch bóng sâu bệnh. Rất đáng giá từng đồng!",
                IsFeatured = true,
                Status = "approved",
                CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 10, 2, 9, 15, 0, DateTimeKind.Utc))
            }
        };
    }
}
