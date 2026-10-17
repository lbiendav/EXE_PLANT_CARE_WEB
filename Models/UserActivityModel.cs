using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class UserActivityModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("path")]
    public string Path { get; set; } = "";

    [FirestoreProperty("title")]
    public string Title { get; set; } = "";

    [FirestoreProperty("action")]
    public string Action { get; set; } = "Xem trang";

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    private DateTime LocalVnTime => (CreatedAt.Kind == DateTimeKind.Utc ? CreatedAt : DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)).AddHours(7);
    public string FormattedTime => LocalVnTime.ToString("HH:mm:ss");
    public string FormattedDate => LocalVnTime.ToString("dd/MM/yyyy");
    public string FormattedDateTime => LocalVnTime.ToString("dd/MM/yyyy HH:mm:ss");
}

public class DailyUserSummaryVM
{
    public string Uid { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public bool IsNew { get; set; }
    public bool IsGuest { get; set; }
    public int SessionCount { get; set; } = 1;
    public int PageViewCount { get; set; } = 1;
    public string LastActiveTime { get; set; } = "";
    public List<UserActivityItemVM> Activities { get; set; } = [];
}

public class UserActivityItemVM
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Action { get; set; } = "Xem trang";
    public string Time { get; set; } = "";
}
