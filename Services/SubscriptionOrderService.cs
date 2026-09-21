using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class SubscriptionOrderService(
    FirestoreService firestore,
    PlanCatalogService catalog,
    ISubscriptionClock clock,
    IConfiguration configuration,
    PaymentModePolicy paymentPolicy)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<SubscriptionOrderModel> Create(string uid, string sku, string requestKey)
    {
        var policy = paymentPolicy.CanCreateCheckout(uid);
        if (!policy.Allowed)
            throw new SubscriptionDomainException(policy.Code, policy.Message);
        if (policy.Mode == PaymentRuntimeMode.Live)
            throw new SubscriptionDomainException("live_provider_not_ready", "Adapter payOS live chưa được kích hoạt. Không có đơn hoặc QR thanh toán thật nào được tạo.");
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100)
            throw new SubscriptionDomainException("invalid_request_key", "Yêu cầu tạo đơn không hợp lệ.");

        var plan = catalog.Get(sku);
        var now = clock.UtcNow;
        var orderRef = _db.Collection("subscription_orders").Document();
        var billingRef = _db.Collection("users").Document(uid).Collection("billing_state").Document("current");
        var requestHash = Hash($"{policy.Mode}:{requestKey}");
        var requestRef = _db.Collection("users").Document(uid).Collection("order_requests").Document(requestHash);
        var subscriptionRef = _db.Collection("subscriptions").Document(uid);

        var order = await _db.RunTransactionAsync(async transaction =>
        {
            var requestSnapshot = await transaction.GetSnapshotAsync(requestRef);
            var billingSnapshot = await transaction.GetSnapshotAsync(billingRef);
            var subscriptionSnapshot = await transaction.GetSnapshotAsync(subscriptionRef);

            if (requestSnapshot.Exists)
            {
                var oldSku = requestSnapshot.GetValue<string>("sku");
                var oldOrderId = requestSnapshot.GetValue<string>("orderId");
                if (!oldSku.Equals(plan.Sku, StringComparison.OrdinalIgnoreCase))
                    throw new SubscriptionDomainException("idempotency_conflict", "Mã yêu cầu này đã được dùng cho lựa chọn khác.");
                var oldOrder = await transaction.GetSnapshotAsync(_db.Collection("subscription_orders").Document(oldOrderId));
                return oldOrder.ConvertTo<SubscriptionOrderModel>();
            }

            if (subscriptionSnapshot.Exists)
            {
                var current = subscriptionSnapshot.ConvertTo<SubscriptionModel>();
                var isActive = current.StartsAt.ToDateTimeOffset() <= now && now < current.ExpiresAt.ToDateTimeOffset();
                if (isActive && !current.Tier.Equals(plan.Tier, StringComparison.OrdinalIgnoreCase))
                    throw new SubscriptionDomainException("tier_change_not_supported", "Bạn chỉ có thể mua hạng khác sau khi gói hiện tại hết hạn.");
            }

            if (billingSnapshot.Exists && billingSnapshot.TryGetValue<string>("pendingOrderId", out var pendingId) && !string.IsNullOrWhiteSpace(pendingId))
            {
                var pendingSnapshot = await transaction.GetSnapshotAsync(_db.Collection("subscription_orders").Document(pendingId));
                if (pendingSnapshot.Exists)
                {
                    var pending = pendingSnapshot.ConvertTo<SubscriptionOrderModel>();
                    if (pending.Status == "Pending" && now < pending.ExpiresAt.ToDateTimeOffset())
                    {
                        if (pending.Sku.Equals(plan.Sku, StringComparison.OrdinalIgnoreCase))
                        {
                            transaction.Set(requestRef, new { orderId = pending.Id, sku = plan.Sku, createdAt = Timestamp.FromDateTimeOffset(now) });
                            return pending;
                        }
                        throw new SubscriptionDomainException("pending_order_exists", "Hãy hủy đơn đang chờ trước khi chọn gói khác.");
                    }
                }
            }

            var expiryMinutes = Math.Clamp(configuration.GetValue<int?>("Payments:OrderExpiryMinutes") ?? 15, 5, 60);
            var snapshot = catalog.Snapshot(plan);
            var bankBin = configuration["Payments:Bank:Bin"]?.Trim() ?? "";
            var bankAccount = configuration["Payments:Bank:AccountNumber"]?.Trim() ?? "";
            var bankName = configuration["Payments:Bank:AccountName"]?.Trim() ?? "";
            var validBank = bankBin.Length is >= 6 and <= 8 && bankBin.All(char.IsDigit) &&
                bankAccount.Length is >= 4 and <= 19 && bankAccount.All(char.IsDigit) &&
                bankName.Length is >= 2 and <= 100;
            var created = new SubscriptionOrderModel
            {
                Id = orderRef.Id,
                UserId = uid,
                Sku = plan.Sku,
                Tier = plan.Tier,
                DurationMonths = plan.DurationMonths,
                AmountVnd = plan.AmountVnd,
                CatalogVersion = snapshot.CatalogVersion,
                PlantLimit = snapshot.PlantLimit,
                MonthlyAiLimit = snapshot.MonthlyAiLimit,
                Status = "Pending",
                CreatedAt = Timestamp.FromDateTimeOffset(now),
                ExpiresAt = Timestamp.FromDateTimeOffset(now.AddMinutes(expiryMinutes)),
                PaymentMode = policy.Mode.ToString(),
                IsDemo = policy.Mode == PaymentRuntimeMode.Demo,
                Provider = "Simulator",
                ChannelId = "demo",
                CheckoutStatus = "Open",
                PaymentStatus = "Unpaid",
                FulfillmentStatus = "NotGranted",
                RefundStatus = "None",
                SchemaVersion = 2,
                TransferReference = $"HP{orderRef.Id[..Math.Min(10, orderRef.Id.Length)].ToUpperInvariant()}",
                BankBin = validBank ? bankBin : "",
                BankAccountNumber = validBank ? bankAccount : "",
                BankAccountName = validBank ? bankName : ""
            };
            transaction.Set(orderRef, created);
            transaction.Set(billingRef, new { pendingOrderId = created.Id, updatedAt = created.CreatedAt });
            transaction.Set(requestRef, new { orderId = created.Id, sku = plan.Sku, createdAt = created.CreatedAt });
            return created;
        });
        return order;
    }

    public async Task<SubscriptionOrderModel?> GetOwned(string uid, string orderId, CancellationToken cancellationToken = default)
    {
        var orderRef = _db.Collection("subscription_orders").Document(orderId);
        var snapshot = await orderRef.GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists) return null;
        var order = snapshot.ConvertTo<SubscriptionOrderModel>();
        return order.UserId == uid ? order : null;
    }

    public async Task<IReadOnlyList<SubscriptionOrderModel>> History(string uid, int page, int pageSize = 10)
    {
        var snapshot = await _db.Collection("subscription_orders")
            .WhereEqualTo("userId", uid)
            .OrderByDescending("createdAt")
            .Offset(Math.Max(0, page - 1) * pageSize)
            .Limit(pageSize)
            .GetSnapshotAsync();
        return snapshot.Documents.Select(x => x.ConvertTo<SubscriptionOrderModel>()).ToArray();
    }

    public async Task<SubscriptionOrderModel> Cancel(string uid, string orderId)
    {
        var orderRef = _db.Collection("subscription_orders").Document(orderId);
        var billingRef = _db.Collection("users").Document(uid).Collection("billing_state").Document("current");
        var now = clock.UtcNow;
        return await _db.RunTransactionAsync(async transaction =>
        {
            var orderSnapshot = await transaction.GetSnapshotAsync(orderRef);
            var billingSnapshot = await transaction.GetSnapshotAsync(billingRef);
            if (!orderSnapshot.Exists || orderSnapshot.GetValue<string>("userId") != uid)
                throw new SubscriptionDomainException("not_found", "Không tìm thấy đơn.");
            var order = orderSnapshot.ConvertTo<SubscriptionOrderModel>();
            if (order.Status != "Pending")
                throw new SubscriptionDomainException("order_terminal", "Đơn này không còn có thể hủy.");
            order.Status = now >= order.ExpiresAt.ToDateTimeOffset() ? "Expired" : "Cancelled";
            order.CheckoutStatus = order.Status;
            transaction.Update(orderRef, new Dictionary<string, object> { ["status"] = order.Status, ["checkoutStatus"] = order.CheckoutStatus });
            if (billingSnapshot.Exists && billingSnapshot.TryGetValue<string>("pendingOrderId", out var pendingId) && pendingId == orderId)
                transaction.Update(billingRef, new Dictionary<string, object> { ["pendingOrderId"] = "", ["updatedAt"] = Timestamp.FromDateTimeOffset(now) });
            return order;
        });
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
