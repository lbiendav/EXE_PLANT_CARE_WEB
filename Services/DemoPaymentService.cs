using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class DemoPaymentService(
    FirestoreService firestore,
    PaymentModePolicy paymentPolicy,
    ISubscriptionClock clock)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<SubscriptionOrderModel> Confirm(string uid, string orderId)
    {
        var now = clock.UtcNow;
        var orderRef = _db.Collection("subscription_orders").Document(orderId);
        var subscriptionRef = _db.Collection("subscriptions").Document(uid);
        var billingRef = _db.Collection("users").Document(uid).Collection("billing_state").Document("current");
        var userRef = _db.Collection("users").Document(uid);
        var eventRef = _db.Collection("payment_events").Document($"demo_{orderId}");

        var result = await _db.RunTransactionAsync(async transaction =>
        {
            var orderSnapshot = await transaction.GetSnapshotAsync(orderRef);
            var userSnapshot = await transaction.GetSnapshotAsync(userRef);
            var subscriptionSnapshot = await transaction.GetSnapshotAsync(subscriptionRef);
            var billingSnapshot = await transaction.GetSnapshotAsync(billingRef);
            var eventSnapshot = await transaction.GetSnapshotAsync(eventRef);

            if (!orderSnapshot.Exists || orderSnapshot.GetValue<string>("userId") != uid)
                return (Order: (SubscriptionOrderModel?)null, Error: "not_found");
            var order = orderSnapshot.ConvertTo<SubscriptionOrderModel>();
            var policy = paymentPolicy.CanSimulate(order);
            if (!policy.Allowed)
                return (Order: order, Error: policy.Code);
            if (order.Status == "Paid" && eventSnapshot.Exists)
                return (Order: order, Error: (string?)null);
            if (!userSnapshot.Exists || (userSnapshot.TryGetValue<bool>("isLocked", out var locked) && locked))
                return (Order: order, Error: "account_inactive");
            if (order.Status != "Pending")
                return (Order: order, Error: "order_terminal");
            if (now >= order.ExpiresAt.ToDateTimeOffset())
            {
                transaction.Update(orderRef, new Dictionary<string, object> { ["status"] = "Expired" });
                if (billingSnapshot.Exists && billingSnapshot.TryGetValue<string>("pendingOrderId", out var pending) && pending == orderId)
                    transaction.Update(billingRef, new Dictionary<string, object> { ["pendingOrderId"] = "", ["updatedAt"] = Timestamp.FromDateTimeOffset(now) });
                order.Status = "Expired";
                return (Order: order, Error: "order_expired");
            }

            var baseTime = now;
            if (subscriptionSnapshot.Exists)
            {
                var current = subscriptionSnapshot.ConvertTo<SubscriptionModel>();
                var active = current.StartsAt.ToDateTimeOffset() <= now && now < current.ExpiresAt.ToDateTimeOffset();
                if (active && !current.Tier.Equals(order.Tier, StringComparison.OrdinalIgnoreCase))
                    return (Order: order, Error: "tier_change_not_supported");
                if (active) baseTime = current.ExpiresAt.ToDateTimeOffset();
            }

            var startsAt = now;
            var expiresAt = SubscriptionTime.AddCalendarMonths(baseTime, order.DurationMonths);
            var paidAt = Timestamp.FromDateTimeOffset(now);
            order.Status = "Paid";
            order.CheckoutStatus = "Closed";
            order.PaymentStatus = "ReceivedExact";
            order.FulfillmentStatus = "Granted";
            order.PaidAt = paidAt;
            order.ActivationStartsAt = Timestamp.FromDateTimeOffset(startsAt);
            order.ActivationExpiresAt = Timestamp.FromDateTimeOffset(expiresAt);

            var subscription = new SubscriptionModel
            {
                UserId = uid,
                Tier = order.Tier,
                StartsAt = Timestamp.FromDateTimeOffset(startsAt),
                ExpiresAt = Timestamp.FromDateTimeOffset(expiresAt),
                DurationMonths = order.DurationMonths,
                CatalogVersion = order.CatalogVersion,
                PlantLimit = order.PlantLimit,
                MonthlyAiLimit = order.MonthlyAiLimit,
                LastOrderId = order.Id,
                IsDemo = true,
                UpdatedAt = paidAt
            };
            transaction.Set(eventRef, new { orderId, userId = uid, mode = "Demo", amountVnd = order.AmountVnd, processedAt = paidAt, isDemo = true });
            transaction.Set(subscriptionRef, subscription);
            transaction.Update(orderRef, new Dictionary<string, object>
            {
                ["status"] = "Paid", ["checkoutStatus"] = "Closed",
                ["paymentStatus"] = "ReceivedExact", ["fulfillmentStatus"] = "Granted", ["paidAt"] = paidAt,
                ["activationStartsAt"] = order.ActivationStartsAt.Value,
                ["activationExpiresAt"] = order.ActivationExpiresAt.Value
            });
            if (billingSnapshot.Exists && billingSnapshot.TryGetValue<string>("pendingOrderId", out var pendingId) && pendingId == orderId)
                transaction.Update(billingRef, new Dictionary<string, object> { ["pendingOrderId"] = "", ["updatedAt"] = paidAt });
            return (Order: (SubscriptionOrderModel?)order, Error: (string?)null);
        });

        if (result.Error != null)
            throw result.Error switch
            {
                "not_found" => new SubscriptionDomainException(result.Error, "Không tìm thấy đơn."),
                "account_inactive" => new SubscriptionDomainException(result.Error, "Tài khoản không còn hoạt động."),
                "order_expired" => new SubscriptionDomainException(result.Error, "Đơn đã hết hạn. Vui lòng tạo đơn mới."),
                "tier_change_not_supported" => new SubscriptionDomainException(result.Error, "Chưa hỗ trợ đổi hạng khi gói hiện tại còn hạn."),
                "live_order_not_simulatable" => new SubscriptionDomainException(result.Error, "Đơn thanh toán thật không thể được xác nhận bằng simulator."),
                "demo_disabled" or "demo_project_not_allowed" => new SubscriptionDomainException(result.Error, "Mô phỏng thanh toán không được phép trong môi trường này."),
                _ => new SubscriptionDomainException(result.Error, "Đơn không còn có thể xác nhận.")
            };
        return result.Order!;
    }
}
