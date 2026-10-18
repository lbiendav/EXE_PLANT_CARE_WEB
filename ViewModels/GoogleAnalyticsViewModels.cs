namespace HomePlant.ViewModels;

public sealed class GoogleAnalyticsOverviewVM
{
    public static GoogleAnalyticsOverviewVM NotConfigured { get; } = new();

    public bool Configured { get; init; }
    public bool Available { get; init; }
    public string Error { get; init; } = "";

    public long ActiveUsers30Days { get; init; }
    public long Sessions30Days { get; init; }
    public long PageViews30Days { get; init; }
    public long NewUsers30Days { get; init; }
    public long ReturningUsers30Days { get; init; }

    public double AverageDailyUsers30Days { get; init; }
    public double AverageDailySessions30Days { get; init; }
    public double AverageDailyPageViews30Days { get; init; }

    public double AverageEngagementSeconds { get; init; }
    public string FormattedAverageEngagement
    {
        get
        {
            var sec = (int)Math.Round(AverageEngagementSeconds);
            if (sec <= 0) return "0s";
            if (sec < 60) return $"{sec}s";
            var min = sec / 60;
            var rem = sec % 60;
            return rem > 0 ? $"{min}m {rem}s" : $"{min}m";
        }
    }

    public double BounceRate { get; init; }
    public string FormattedBounceRate => $"{BounceRate * 100:0.0}%";

    public IReadOnlyList<AnalyticsRowVM> TrafficSources { get; init; } = [];
    public IReadOnlyList<AnalyticsPageDetailVM> PopularPages { get; init; } = [];
    public IReadOnlyList<AnalyticsPageDetailVM> LeastVisitedPages { get; init; } = [];
    public IReadOnlyList<AnalyticsDailyRowVM> DailyTrends { get; init; } = [];
    public IReadOnlyList<AnalyticsRowVM> DeviceBreakdown { get; init; } = [];
    public IReadOnlyList<AnalyticsLandingPageVM> LandingPages { get; init; } = [];
    public IReadOnlyList<AnalyticsRowVM> Countries { get; init; } = [];
}

public sealed record AnalyticsRowVM(string Label, long Value);

public sealed record AnalyticsDailyRowVM(
    string DateKey,
    string DisplayDate,
    long ActiveUsers,
    long Sessions,
    long PageViews,
    long NewUsers = 0,
    long ReturningUsers = 0);

public sealed record AnalyticsPageDetailVM(
    string Path,
    string Title,
    long Views,
    long Users,
    double TotalDurationSeconds)
{
    public string Label => string.IsNullOrWhiteSpace(Title) ? Path : (Path == "/" ? Title : $"{Title} · {Path}");
    public long Value => Views;
    public double AverageDurationSeconds => Users > 0 ? TotalDurationSeconds / Users : (Views > 0 ? TotalDurationSeconds / Views : 0);
    public string FormattedDuration
    {
        get
        {
            var sec = (int)Math.Round(AverageDurationSeconds);
            if (sec <= 0) return "0s";
            if (sec < 60) return $"{sec}s";
            var min = sec / 60;
            var rem = sec % 60;
            return rem > 0 ? $"{min}m {rem}s" : $"{min}m";
        }
    }
}

public sealed record AnalyticsLandingPageVM(
    string Path,
    string Title,
    long Sessions,
    double BounceRate)
{
    public string FormattedBounceRate => $"{BounceRate * 100:0.0}%";
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
    public List<UserPageVisitSummaryVM> PageSummaries { get; set; } = [];
    public int TotalDurationSeconds { get; set; }
    public string FormattedTotalDuration { get; set; } = "";
}

public class UserPageVisitSummaryVM
{
    public string Category { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public int VisitCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string FormattedDuration { get; set; } = "";
    public List<string> VisitTimes { get; set; } = [];
}

public class UserActivityItemVM
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Action { get; set; } = "Xem trang";
    public string Time { get; set; } = "";
    public int DurationSeconds { get; set; }
    public string FormattedDuration { get; set; } = "";
}
