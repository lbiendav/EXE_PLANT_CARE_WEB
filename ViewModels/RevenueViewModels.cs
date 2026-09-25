using HomePlant.Models;

namespace HomePlant.ViewModels;

public sealed class RevenueDashboardVM
{
    public string Tab { get; init; } = "overview";
    public string Query { get; init; } = "";
    public string Status { get; init; } = "";
    public string Detail { get; init; } = "";
    public string DetailTitle { get; init; } = "";
    public string DetailDescription { get; init; } = "";
    public long RevenueToday { get; init; }
    public long Revenue7Days { get; init; }
    public long Revenue30Days { get; init; }
    public int TotalAccounts { get; init; }
    public int NewAccounts30Days { get; init; }
    public int ActiveSubscriptions { get; init; }
    public int PayingCustomers { get; init; }
    public int SuccessfulOrders { get; init; }
    public int FailedOrders { get; init; }
    public int PendingOrders { get; init; }
    public int NeedsReviewOrders { get; init; }
    public int UnmatchedTransactions { get; init; }
    public double AbandonmentRate { get; init; }
    public IReadOnlyDictionary<string, int> UsersByTier { get; init; } = new Dictionary<string, int>();
    public string BestSeller { get; init; } = "Chưa có dữ liệu";
    public IReadOnlyList<RevenueOrderRowVM> Orders { get; init; } = [];
    public IReadOnlyList<RevenueAccountDetailVM> DetailAccounts { get; init; } = [];
    public IReadOnlyList<RevenueSubscriptionRowVM> DetailSubscriptions { get; init; } = [];
    public IReadOnlyList<RevenueOrderRowVM> DetailOrders { get; init; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> DetailUnmatchedPayments { get; init; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> UnmatchedPayments { get; init; } = [];
    public IReadOnlyList<RevenueSubscriptionRowVM> Subscriptions { get; init; } = [];
    public IReadOnlyList<RevenuePlanRowVM> Plans { get; init; } = [];
    public IReadOnlyList<RevenueAuditRowVM> AuditLogs { get; init; } = [];
    public bool PayOsConfigured { get; init; }
    public GoogleAnalyticsOverviewVM Analytics { get; init; } = GoogleAnalyticsOverviewVM.NotConfigured;
}

public sealed class GoogleAnalyticsOverviewVM
{
    public static GoogleAnalyticsOverviewVM NotConfigured { get; } = new();
    public bool Configured { get; init; }
    public bool Available { get; init; }
    public string Error { get; init; } = "";
    public long ActiveUsers30Days { get; init; }
    public long Sessions30Days { get; init; }
    public long PageViews30Days { get; init; }
    public IReadOnlyList<AnalyticsRowVM> TrafficSources { get; init; } = [];
    public IReadOnlyList<AnalyticsRowVM> PopularPages { get; init; } = [];
}

public sealed record AnalyticsRowVM(string Label, long Value);

public sealed record RevenueOrderRowVM(SubscriptionOrderModel Order, string Email, string DisplayName, long ReceivedAmount, string ReviewReason);
public sealed record RevenueAccountDetailVM(UserModel User, int SuccessfulOrders, long PaidAmount);
public sealed record RevenueSubscriptionRowVM(string UserId, string Email, string DisplayName, SubscriptionModel? Subscription, SubscriptionUsage Usage);
public sealed record RevenuePlanRowVM(PlanDefinition Plan, bool Enabled, string Description, string Benefits, string Version);
public sealed record RevenueAuditRowVM(string Id, string AdminEmail, string Action, string TargetType, string TargetId, string Reason, DateTimeOffset CreatedAt, string Before, string After);

public sealed class RevenueOrderDetailVM
{
    public required RevenueOrderRowVM Row { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Transactions { get; init; } = [];
    public IReadOnlyList<RevenueAuditRowVM> AuditLogs { get; init; } = [];
}

public sealed class RevenueCustomerVM
{
    public required UserModel User { get; init; }
    public SubscriptionModel? Subscription { get; init; }
    public required SubscriptionUsage Usage { get; init; }
    public IReadOnlyList<RevenueOrderRowVM> Orders { get; init; } = [];
    public IReadOnlyList<RevenueAuditRowVM> History { get; init; } = [];
    public IReadOnlyList<(string Author, string Note, DateTimeOffset CreatedAt)> Notes { get; init; } = [];
}
