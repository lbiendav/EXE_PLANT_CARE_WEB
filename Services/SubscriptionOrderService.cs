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
    PaymentModePolicy paymentPolicy,
    IPaymentProvider paymentProvider)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<SubscriptionOrderModel> Create(string uid, string sku, string requestKey)
    {
        var policy = paymentPolicy.CanCreateCheckout(uid);
        if (!policy.Allowed)
            throw new SubscriptionDomainException(policy.Code, policy.Message);
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100)
            throw new SubscriptionDomainException("invalid_request_key", "Yêu cầu tạo đơn không hợp lệ.");

        var plan = catalog.Get(sku);
        var planSetting = await _db.Collection("plan_settings").Document(plan.Sku).GetSnapshotAsync();
        if (planSetting.Exists && planSetting.TryGetValue<bool>("enabled", out var planEnabled) && !planEnabled)
            throw new SubscriptionDomainException("plan_disabled", "Gói này đang tạm ngừng đăng ký.");
        var now = clock.UtcNow;
        var orderRef = _db.Collection("subscription_orders").Document();
        var billingScope = policy.Mode == PaymentRuntimeMode.Live ? "live" : "demo";
        var billingRef = _db.Collection("users").Document(uid).Collection("billing_state").Document(billingScope);
        var requestHash = Hash($"{policy.Mode}:{requestKey}");
        var requestRef = _db.Collection("users").Document(uid).Collection("order_requests").Document(requestHash);
        var subscriptionRef = _db.Collection("subscriptions").Document(uid);

        var result = await _db.RunTransactionAsync(async transaction =>
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
                return (Order: oldOrder.ConvertTo<SubscriptionOrderModel>(), IsNew: false);
            }

            if (subscriptionSnapshot.Exists)
            {
                var current = subscriptionSnapshot.ConvertTo<SubscriptionModel>();
                var sameMode = policy.Mode == PaymentRuntimeMode.Live ? !current.IsDemo : current.IsDemo;
                var isActive = sameMode && current.StartsAt.ToDateTimeOffset() <= now && now < current.ExpiresAt.ToDateTimeOffset();
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
                            return (Order: pending, IsNew: false);
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
            var isLive = policy.Mode == PaymentRuntimeMode.Live;
            var channelId = isLive ? configuration["Payments:PayOS:ChannelId"]!.Trim() : "demo";
            var providerOrderCode = isLive ? ProviderOrderCode(orderRef.Id) : 0;
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
                Provider = isLive ? "PayOS" : "Simulator",
                ChannelId = channelId,
                ProviderOrderCode = providerOrderCode,
                IsPilot = isLive && (configuration.GetValue<bool?>("Payments:PilotOnly") ?? true),
                CheckoutStatus = isLive ? "Initializing" : "Open",
                PaymentStatus = "Unpaid",
                FulfillmentStatus = "NotGranted",
                RefundStatus = "None",
                SchemaVersion = 2,
                TransferReference = $"HP{orderRef.Id[..Math.Min(10, orderRef.Id.Length)].ToUpperInvariant()}",
                BankBin = !isLive && validBank ? bankBin : "",
                BankAccountNumber = !isLive && validBank ? bankAccount : "",
                BankAccountName = !isLive && validBank ? bankName : ""
            };
            if (isLive)
            {
                var mappingRef = _db.Collection("payment_provider_orders").Document(LivePaymentService.MappingId(channelId, providerOrderCode));
                var mappingSnapshot = await transaction.GetSnapshotAsync(mappingRef);
                if (mappingSnapshot.Exists)
                    throw new SubscriptionDomainException("provider_order_collision", "Không thể cấp mã thanh toán duy nhất. Vui lòng thử lại.");
                transaction.Set(mappingRef, new { orderId = created.Id, userId = uid, channelId, providerOrderCode, createdAt = created.CreatedAt });
            }
            transaction.Set(orderRef, created);
            transaction.Set(billingRef, new { pendingOrderId = created.Id, updatedAt = created.CreatedAt });
            transaction.Set(requestRef, new { orderId = created.Id, sku = plan.Sku, createdAt = created.CreatedAt });
            return (Order: created, IsNew: true);
        });

        if (policy.Mode != PaymentRuntimeMode.Live || !result.IsNew)
            return result.Order;

        var publicBaseUrl = configuration["App:PublicBaseUrl"]!.Trim().TrimEnd('/');
        try
        {
            var checkout = await paymentProvider.CreateCheckout(new ProviderCheckoutRequest(
                result.Order.Id,
                result.Order.ProviderOrderCode,
                result.Order.AmountVnd,
                PayOsDescription(result.Order.ProviderOrderCode),
                result.Order.ExpiresAt.ToDateTimeOffset(),
                $"{publicBaseUrl}/Checkout/{result.Order.Id}/Return",
                $"{publicBaseUrl}/Checkout/{result.Order.Id}/Return"));
            if (checkout.OrderCode != result.Order.ProviderOrderCode || checkout.AmountVnd != result.Order.AmountVnd ||
                !string.Equals(checkout.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(checkout.PaymentLinkId) ||
                !Uri.TryCreate(checkout.CheckoutUrl, UriKind.Absolute, out var checkoutUri) || checkoutUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("payOS returned checkout data that does not match the order snapshot.");
            result.Order.ProviderPaymentLinkId = checkout.PaymentLinkId;
            result.Order.ProviderCheckoutUrl = checkout.CheckoutUrl;
            result.Order.ProviderQrCode = checkout.QrCode;
            result.Order.RawProviderStatus = checkout.Status;
            result.Order.CheckoutStatus = "Open";
            result.Order.TransferReference = checkout.Description;
            result.Order.BankBin = checkout.Bin;
            result.Order.BankAccountNumber = checkout.AccountNumber;
            result.Order.BankAccountName = checkout.AccountName;
            await _db.Collection("subscription_orders").Document(result.Order.Id).UpdateAsync(new Dictionary<string, object>
            {
                ["providerPaymentLinkId"] = checkout.PaymentLinkId,
                ["providerCheckoutUrl"] = checkout.CheckoutUrl,
                ["providerQrCode"] = checkout.QrCode,
                ["rawProviderStatus"] = checkout.Status,
                ["checkoutStatus"] = "Open",
                ["transferReference"] = checkout.Description,
                ["bankBin"] = checkout.Bin,
                ["bankAccountNumber"] = checkout.AccountNumber,
                ["bankAccountName"] = checkout.AccountName
            });
            return result.Order;
        }
        catch
        {
            await _db.Collection("subscription_orders").Document(result.Order.Id).UpdateAsync(new Dictionary<string, object>
            {
                ["checkoutStatus"] = "CreateUnknown", ["rawProviderStatus"] = "CREATE_UNKNOWN"
            });
            throw new SubscriptionDomainException("payment_provider_unavailable", "Chưa thể tạo phiên thanh toán payOS. Không nên chuyển khoản cho đến khi đơn hiển thị link payOS hợp lệ.");
        }
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
        var existing = await GetOwned(uid, orderId) ?? throw new SubscriptionDomainException("not_found", "Không tìm thấy đơn.");
        if (!existing.IsDemo && existing.Provider == "PayOS" && existing.ProviderOrderCode > 0 && existing.CheckoutStatus == "Open")
        {
            try { await paymentProvider.CancelCheckout(existing.ProviderOrderCode, "User requested cancellation"); }
            catch { throw new SubscriptionDomainException("provider_cancel_failed", "Chưa thể hủy phiên payOS. Vui lòng thử lại để tránh thanh toán nhầm đơn."); }
        }
        var orderRef = _db.Collection("subscription_orders").Document(orderId);
        var billingRef = _db.Collection("users").Document(uid).Collection("billing_state").Document(existing.IsDemo ? "demo" : "live");
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
    private static long ProviderOrderCode(string orderId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(orderId));
        return (long)(BitConverter.ToUInt64(hash, 0) & 0x001F_FFFF_FFFF_FFFFUL);
    }

    private static string PayOsDescription(long orderCode) => $"HP{orderCode % 10_000_000:D7}";
}
