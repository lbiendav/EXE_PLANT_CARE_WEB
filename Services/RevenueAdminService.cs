using System.Text.Json;
using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.ViewModels;

namespace HomePlant.Services;

public sealed class RevenueAdminService(
    FirestoreService firestore,
    PlanCatalogService catalog,
    ISubscriptionClock clock,
    UsageService usageService,
    SubscriptionOrderService orderService,
    IPaymentProvider provider,
    LivePaymentService livePayments,
    EmailNotificationService emailService,
    IConfiguration configuration)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<RevenueDashboardVM> Dashboard(string tab, string query, string status)
    {
        var usersTask = _db.Collection("users").GetSnapshotAsync();
        var ordersTask = _db.Collection("subscription_orders").GetSnapshotAsync();
        var subscriptionsTask = _db.Collection("subscriptions").GetSnapshotAsync();
        var transactionsTask = _db.Collection("payment_transactions").GetSnapshotAsync();
        var plansTask = _db.Collection("plan_settings").GetSnapshotAsync();
        var auditTask = _db.Collection("revenue_audit_logs").OrderByDescending("createdAt").Limit(100).GetSnapshotAsync();
        await Task.WhenAll(usersTask, ordersTask, subscriptionsTask, transactionsTask, plansTask, auditTask);

        var users = usersTask.Result.Documents.Select(x => x.ConvertTo<UserModel>()).ToList();
        var usersById = users.ToDictionary(x => x.Id, x => x);
        var subscriptions = subscriptionsTask.Result.Documents.Select(x => x.ConvertTo<SubscriptionModel>()).ToDictionary(x => x.UserId, x => x);
        var transactions = transactionsTask.Result.Documents.Select(ToDictionary).ToList();
        var receivedByOrder = transactions.Where(x => Text(x, "orderId").Length > 0)
            .GroupBy(x => Text(x, "orderId")).ToDictionary(x => x.Key, x => x.Sum(y => Number(y, "amountVnd")));
        var reasonByOrder = transactions.Where(x => Text(x, "orderId").Length > 0 && Text(x, "status") == "NeedsReview")
            .GroupBy(x => Text(x, "orderId")).ToDictionary(x => x.Key, x => Text(x.OrderByDescending(y => Date(y, "receivedAt")).First(), "reason"));
        var allOrders = ordersTask.Result.Documents.Select(x => x.ConvertTo<SubscriptionOrderModel>()).OrderByDescending(x => x.CreatedAt).ToList();
        var rows = allOrders.Select(order =>
        {
            usersById.TryGetValue(order.UserId, out var user);
            return new RevenueOrderRowVM(order, user?.Email ?? "", user?.FullName ?? "", receivedByOrder.GetValueOrDefault(order.Id), reasonByOrder.GetValueOrDefault(order.Id, ""));
        }).ToList();
        if (!string.IsNullOrWhiteSpace(query))
            rows = rows.Where(x => new[] { x.Order.Id, x.Order.UserId, x.Order.ProviderOrderCode.ToString(), x.Order.ProviderPaymentLinkId, x.Order.TransferReference, x.Email, x.DisplayName }
                .Any(value => value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        if (!string.IsNullOrWhiteSpace(status))
            rows = rows.Where(x => EffectiveStatus(x.Order).Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

        var subscriptionRows = new List<RevenueSubscriptionRowVM>();
        if (tab is "subscriptions" or "customers") foreach (var user in users.OrderBy(x => x.Email))
        {
            subscriptions.TryGetValue(user.Id, out var subscription);
            var usage = await usageService.Get(user.Id);
            subscriptionRows.Add(new RevenueSubscriptionRowVM(user.Id, user.Email, user.FullName, subscription, usage));
        }
        if (!string.IsNullOrWhiteSpace(query))
            subscriptionRows = subscriptionRows.Where(x => x.UserId.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Email.Contains(query, StringComparison.OrdinalIgnoreCase) || x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var planSettings = plansTask.Result.Documents.ToDictionary(x => x.Id, ToDictionary, StringComparer.OrdinalIgnoreCase);
        var planRows = catalog.GetAll().OrderBy(x => x.AmountVnd).Select(plan =>
        {
            planSettings.TryGetValue(plan.Sku, out var setting);
            return new RevenuePlanRowVM(plan, setting == null || Bool(setting, "enabled", true), Text(setting, "description"), Text(setting, "benefits"), Text(setting, "version") is { Length: > 0 } v ? v : PlanCatalogService.Version);
        }).ToList();

        var paid = allOrders.Where(x => x.Status == "Paid" && x.PaidAt != null).ToList();
        var now = clock.UtcNow;
        var nonPaid = allOrders.Count(x => x.Status is "Cancelled" or "Expired" || x.FulfillmentStatus == "HeldForReview");
        var tierCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Free"] = users.Count - subscriptions.Count, ["Silver"] = 0, ["Gold"] = 0 };
        foreach (var subscription in subscriptions.Values) tierCounts[subscription.Tier] = tierCounts.GetValueOrDefault(subscription.Tier) + 1;
        return new RevenueDashboardVM
        {
            Tab = tab, Query = query, Status = status,
            RevenueToday = SumSince(paid, StartOfVietnamDay(now)), Revenue7Days = SumSince(paid, now.AddDays(-7)), Revenue30Days = SumSince(paid, now.AddDays(-30)),
            SuccessfulOrders = paid.Count, FailedOrders = nonPaid, PendingOrders = allOrders.Count(x => x.Status == "Pending"),
            NeedsReviewOrders = allOrders.Count(x => x.PaymentStatus == "NeedsReview" || x.FulfillmentStatus == "HeldForReview"),
            UnmatchedTransactions = transactions.Count(x => Text(x, "status") == "NeedsReview" && string.IsNullOrWhiteSpace(Text(x, "orderId"))),
            AbandonmentRate = allOrders.Count == 0 ? 0 : nonPaid * 100d / allOrders.Count,
            UsersByTier = tierCounts,
            BestSeller = paid.GroupBy(x => x.Sku).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} · {x.Count()} đơn").FirstOrDefault() ?? "Chưa có dữ liệu",
            Orders = rows.Take(200).ToList(), Subscriptions = subscriptionRows.Take(200).ToList(), Plans = planRows,
            UnmatchedPayments = transactions.Where(x => Text(x, "status") == "NeedsReview" && string.IsNullOrWhiteSpace(Text(x, "orderId"))).Take(50).ToList(),
            AuditLogs = auditTask.Result.Documents.Select(ToAudit).ToList(),
            PayOsConfigured = new[] { "ClientId", "ApiKey", "ChecksumKey" }.All(key => !string.IsNullOrWhiteSpace(configuration[$"Payments:PayOS:{key}"]))
        };
    }

    public async Task<RevenueOrderDetailVM?> OrderDetail(string id)
    {
        var orderSnapshot = await _db.Collection("subscription_orders").Document(id).GetSnapshotAsync();
        if (!orderSnapshot.Exists) return null;
        var order = orderSnapshot.ConvertTo<SubscriptionOrderModel>();
        var userTask = _db.Collection("users").Document(order.UserId).GetSnapshotAsync();
        var txTask = _db.Collection("payment_transactions").WhereEqualTo("orderId", id).GetSnapshotAsync();
        var auditTask = _db.Collection("revenue_audit_logs").WhereEqualTo("targetId", id).GetSnapshotAsync();
        await Task.WhenAll(userTask, txTask, auditTask);
        var user = userTask.Result.Exists ? userTask.Result.ConvertTo<UserModel>() : null;
        var tx = txTask.Result.Documents.Select(ToDictionary).OrderByDescending(x => Date(x, "receivedAt")).ToList();
        return new RevenueOrderDetailVM
        {
            Row = new RevenueOrderRowVM(order, user?.Email ?? "", user?.FullName ?? "", tx.Sum(x => Number(x, "amountVnd")), tx.FirstOrDefault(x => Text(x, "status") == "NeedsReview") is { } review ? Text(review, "reason") : ""),
            Transactions = tx, AuditLogs = auditTask.Result.Documents.Select(ToAudit).OrderByDescending(x => x.CreatedAt).ToList()
        };
    }

    public async Task CancelOrder(string orderId, string adminUid, string adminEmail, string reason)
    {
        RequireReason(reason);
        var before = await _db.Collection("subscription_orders").Document(orderId).GetSnapshotAsync();
        if (!before.Exists) throw new SubscriptionDomainException("not_found", "Không tìm thấy đơn.");
        var order = before.ConvertTo<SubscriptionOrderModel>();
        await orderService.Cancel(order.UserId, orderId);
        await Audit(adminUid, adminEmail, "CancelOrder", "order", orderId, reason, SafeJson(order), new { status = "Cancelled" });
    }

    public async Task SyncOrder(string orderId, string adminUid, string adminEmail, string reason)
    {
        RequireReason(reason);
        var snapshot = await _db.Collection("subscription_orders").Document(orderId).GetSnapshotAsync();
        if (!snapshot.Exists) throw new SubscriptionDomainException("not_found", "Không tìm thấy đơn.");
        var order = snapshot.ConvertTo<SubscriptionOrderModel>();
        if (order.IsDemo || order.ProviderOrderCode <= 0) throw new SubscriptionDomainException("not_live", "Đơn này không phải đơn payOS thật.");
        var state = await provider.GetCheckout(order.ProviderOrderCode);
        if (state.OrderCode != order.ProviderOrderCode || state.PaymentLinkId != order.ProviderPaymentLinkId)
            throw new SubscriptionDomainException("provider_mismatch", "Dữ liệu payOS không khớp đơn HomePlant.");
        PaymentIngestionResult? ingestion = null;
        if (state.Payment != null) ingestion = await livePayments.Reprocess(state.Payment);
        await _db.Collection("subscription_orders").Document(orderId).UpdateAsync(new Dictionary<string, object> { ["rawProviderStatus"] = state.Status, ["lastSyncedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
        await Audit(adminUid, adminEmail, "SyncPayOS", "order", orderId, reason, new { order.RawProviderStatus }, new { ProviderStatus = state.Status, state.AmountPaid, IngestionStatus = ingestion?.Status });
    }

    public async Task Grant(string userId, string sku, int months, string adminUid, string adminEmail, string reason)
    {
        RequireReason(reason);
        var plan = catalog.Get(sku);
        if (months is < 1 or > 36) throw new SubscriptionDomainException("invalid_duration", "Thời hạn phải từ 1 đến 36 tháng.");
        var userRef = _db.Collection("users").Document(userId);
        var subscriptionRef = _db.Collection("subscriptions").Document(userId);
        var historyRef = _db.Collection("subscription_history").Document();
        var auditRef = _db.Collection("revenue_audit_logs").Document();
        var now = clock.UtcNow;
        await _db.RunTransactionAsync(async transaction =>
        {
            var user = await transaction.GetSnapshotAsync(userRef);
            var current = await transaction.GetSnapshotAsync(subscriptionRef);
            if (!user.Exists) throw new SubscriptionDomainException("not_found", "Không tìm thấy người dùng.");
            var starts = Timestamp.FromDateTimeOffset(now);
            var baseTime = current.Exists && current.ConvertTo<SubscriptionModel>().ExpiresAt.ToDateTimeOffset() > now ? current.ConvertTo<SubscriptionModel>().ExpiresAt.ToDateTimeOffset() : now;
            var model = new SubscriptionModel { UserId = userId, Tier = plan.Tier, StartsAt = starts, ExpiresAt = Timestamp.FromDateTimeOffset(SubscriptionTime.AddCalendarMonths(baseTime, months)), DurationMonths = months, CatalogVersion = plan.CatalogVersion(), PlantLimit = plan.PlantLimit, MonthlyAiLimit = plan.MonthlyAiLimit, LastOrderId = $"admin:{auditRef.Id}", IsDemo = false, UpdatedAt = starts, SchemaVersion = 2 };
            transaction.Set(subscriptionRef, model);
            transaction.Set(historyRef, new { userId, action = "AdminGrant", tier = plan.Tier, months, startsAt = starts, expiresAt = model.ExpiresAt, adminUid, reason = reason.Trim(), createdAt = starts });
            transaction.Set(auditRef, AuditData(adminUid, adminEmail, "GrantSubscription", "subscription", userId, reason, current.Exists ? SafeJson(current.ConvertTo<SubscriptionModel>()) : "{}", SafeJson(model), now));
        });
    }

    public async Task Revoke(string userId, string adminUid, string adminEmail, string reason)
    {
        RequireReason(reason);
        var subscriptionRef = _db.Collection("subscriptions").Document(userId);
        var historyRef = _db.Collection("subscription_history").Document();
        var auditRef = _db.Collection("revenue_audit_logs").Document();
        var now = clock.UtcNow;
        await _db.RunTransactionAsync(async transaction =>
        {
            var current = await transaction.GetSnapshotAsync(subscriptionRef);
            if (!current.Exists) throw new SubscriptionDomainException("not_found", "Người dùng không có thuê bao để thu hồi.");
            var before = current.ConvertTo<SubscriptionModel>();
            transaction.Delete(subscriptionRef);
            transaction.Set(historyRef, new { userId, action = "AdminRevoke", tier = before.Tier, adminUid, reason = reason.Trim(), createdAt = Timestamp.FromDateTimeOffset(now) });
            transaction.Set(auditRef, AuditData(adminUid, adminEmail, "RevokeSubscription", "subscription", userId, reason, SafeJson(before), "{}", now));
        });
    }

    public async Task UpdatePlan(string sku, bool enabled, string description, string benefits, string adminUid, string adminEmail, string reason)
    {
        RequireReason(reason); var plan = catalog.Get(sku); var reference = _db.Collection("plan_settings").Document(plan.Sku); var before = await reference.GetSnapshotAsync();
        var version = $"{PlanCatalogService.Version}-{clock.UtcNow:yyyyMMddHHmmss}";
        var data = new Dictionary<string, object> { ["enabled"] = enabled, ["description"] = (description ?? "").Trim(), ["benefits"] = (benefits ?? "").Trim(), ["version"] = version, ["updatedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow), ["updatedBy"] = adminUid };
        await reference.SetAsync(data); await Audit(adminUid, adminEmail, "UpdatePlan", "plan", sku, reason, before.Exists ? SafeJson(ToDictionary(before)) : "{}", data);
    }

    public async Task EnsurePlanEnabled(string sku)
    {
        var snapshot = await _db.Collection("plan_settings").Document(sku).GetSnapshotAsync();
        if (snapshot.Exists && snapshot.TryGetValue<bool>("enabled", out var enabled) && !enabled) throw new SubscriptionDomainException("plan_disabled", "Gói này đang tạm ngừng đăng ký.");
    }

    public async Task AddNote(string userId, string adminUid, string adminEmail, string note)
    {
        RequireReason(note); await _db.Collection("users").Document(userId).Collection("support_notes").AddAsync(new { note = note.Trim(), authorUid = adminUid, authorEmail = adminEmail, createdAt = Timestamp.FromDateTimeOffset(clock.UtcNow) });
        await Audit(adminUid, adminEmail, "AddSupportNote", "user", userId, note, new { }, new { note = note.Trim() });
    }

    public async Task ResendReceipt(string orderId, string adminUid, string adminEmail, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var detail = await OrderDetail(orderId) ?? throw new SubscriptionDomainException("not_found", "Không tìm thấy đơn.");
        if (detail.Row.Order.Status != "Paid") throw new SubscriptionDomainException("not_paid", "Chỉ gửi xác nhận cho đơn đã thanh toán.");
        if (string.IsNullOrWhiteSpace(detail.Row.Email)) throw new SubscriptionDomainException("email_missing", "Khách hàng chưa có email hợp lệ.");
        var order = detail.Row.Order;
        var sent = await emailService.SendTransactional(detail.Row.Email, $"HomePlant · Xác nhận thanh toán {order.TransferReference}",
            $"HomePlant xác nhận đơn {order.TransferReference} đã thanh toán {order.AmountVnd:N0}đ. Gói {order.Tier} trong {order.DurationMonths} tháng đã được ghi nhận.", cancellationToken);
        if (!sent) throw new SubscriptionDomainException("email_failed", "Chưa gửi được email. Vui lòng kiểm tra cấu hình email và thử lại.");
        await Audit(adminUid, adminEmail, "ResendPaymentReceipt", "order", orderId, reason, new { }, new { recipient = detail.Row.Email });
    }

    private async Task Audit(string uid, string email, string action, string targetType, string targetId, string reason, object before, object after) =>
        await _db.Collection("revenue_audit_logs").AddAsync(AuditData(uid, email, action, targetType, targetId, reason, before is string s ? s : SafeJson(before), after is string a ? a : SafeJson(after), clock.UtcNow));
    private static Dictionary<string, object> AuditData(string uid, string email, string action, string targetType, string targetId, string reason, string before, string after, DateTimeOffset now) => new() { ["adminUid"] = uid, ["adminEmail"] = email, ["action"] = action, ["targetType"] = targetType, ["targetId"] = targetId, ["reason"] = reason.Trim(), ["before"] = before, ["after"] = after, ["createdAt"] = Timestamp.FromDateTimeOffset(now) };
    private static void RequireReason(string? value) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 5) throw new SubscriptionDomainException("reason_required", "Vui lòng nhập lý do tối thiểu 5 ký tự."); }
    private static string EffectiveStatus(SubscriptionOrderModel x) => x.PaymentStatus == "NeedsReview" || x.FulfillmentStatus == "HeldForReview" ? "NeedsReview" : x.Status;
    private static long SumSince(IEnumerable<SubscriptionOrderModel> orders, DateTimeOffset since) => orders.Where(x => x.PaidAt?.ToDateTimeOffset() >= since).Sum(x => x.AmountVnd);
    private static DateTimeOffset StartOfVietnamDay(DateTimeOffset now) { var local = now.ToOffset(TimeSpan.FromHours(7)); return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset); }
    private static Dictionary<string, object?> ToDictionary(DocumentSnapshot snapshot) => snapshot.ToDictionary().ToDictionary(x => x.Key, x => (object?)x.Value);
    private static string Text(IReadOnlyDictionary<string, object?>? x, string key) => x != null && x.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    private static long Number(IReadOnlyDictionary<string, object?> x, string key) => x.TryGetValue(key, out var value) ? Convert.ToInt64(value) : 0;
    private static bool Bool(IReadOnlyDictionary<string, object?> x, string key, bool fallback) => x.TryGetValue(key, out var value) && value is bool result ? result : fallback;
    private static DateTimeOffset Date(IReadOnlyDictionary<string, object?> x, string key) => x.TryGetValue(key, out var value) && value is Timestamp ts ? ts.ToDateTimeOffset() : DateTimeOffset.MinValue;
    private static RevenueAuditRowVM ToAudit(DocumentSnapshot x) { var d = ToDictionary(x); return new(x.Id, Text(d, "adminEmail"), Text(d, "action"), Text(d, "targetType"), Text(d, "targetId"), Text(d, "reason"), Date(d, "createdAt"), Text(d, "before"), Text(d, "after")); }
    private static string SafeJson(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
}

internal static class PlanDefinitionExtensions
{
    public static string CatalogVersion(this PlanDefinition _) => PlanCatalogService.Version;
}
