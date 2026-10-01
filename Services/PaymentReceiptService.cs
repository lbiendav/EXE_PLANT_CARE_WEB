using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class PaymentReceiptService(
    FirestoreService firestore,
    EmailNotificationService email,
    IConfiguration configuration,
    ILogger<PaymentReceiptService> logger)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task SendOnce(string orderId, CancellationToken cancellationToken = default)
    {
        var orderRef = _db.Collection("subscription_orders").Document(orderId);
        var deliveryRef = _db.Collection("payment_receipt_deliveries").Document(orderId);
        var userId = "";
        var recipient = "";
        SubscriptionOrderModel? order = null;
        var now = DateTimeOffset.UtcNow;

        var claimed = await _db.RunTransactionAsync(async transaction =>
        {
            var orderSnapshot = await transaction.GetSnapshotAsync(orderRef);
            var deliverySnapshot = await transaction.GetSnapshotAsync(deliveryRef);
            if (!orderSnapshot.Exists) return false;

            order = orderSnapshot.ConvertTo<SubscriptionOrderModel>();
            if (order.Status != "Paid" || order.FulfillmentStatus != "Granted") return false;
            userId = order.UserId;

            if (deliverySnapshot.Exists)
            {
                var status = deliverySnapshot.TryGetValue<string>("status", out var currentStatus) ? currentStatus : "";
                if (status == "Sent") return false;
                if (status == "Sending" &&
                    deliverySnapshot.TryGetValue<Timestamp>("attemptedAt", out var attemptedAt) &&
                    attemptedAt.ToDateTimeOffset() > now.AddMinutes(-5)) return false;
            }

            transaction.Set(deliveryRef, new Dictionary<string, object>
            {
                ["orderId"] = orderId,
                ["userId"] = userId,
                ["status"] = "Sending",
                ["attemptedAt"] = Timestamp.FromDateTimeOffset(now),
                ["schemaVersion"] = 1
            }, SetOptions.MergeAll);
            return true;
        });

        if (!claimed || order is null) return;

        try
        {
            var userSnapshot = await _db.Collection("users").Document(userId).GetSnapshotAsync(cancellationToken);
            if (userSnapshot.Exists && userSnapshot.TryGetValue<string>("email", out var emailAddress))
                recipient = emailAddress.Trim();

            if (string.IsNullOrWhiteSpace(recipient))
            {
                await Mark(deliveryRef, "Failed", "email_missing", now, cancellationToken);
                logger.LogWarning("Payment receipt was not sent because order {OrderId} has no recipient email.", orderId);
                return;
            }

            var sent = await email.SendTransactional(
                recipient,
                $"HomePlant · Biên nhận thanh toán {order.TransferReference}",
                BuildReceipt(order),
                cancellationToken);

            await Mark(deliveryRef, sent ? "Sent" : "Failed", sent ? "" : "provider_rejected", now, cancellationToken);
            if (!sent) logger.LogWarning("Payment receipt delivery failed for order {OrderId}.", orderId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await Mark(deliveryRef, "Failed", "delivery_exception", now, CancellationToken.None);
            logger.LogWarning(exception, "Payment receipt delivery failed for order {OrderId}.", orderId);
        }
    }

    private string BuildReceipt(SubscriptionOrderModel order)
    {
        var paidAt = (order.PaidAt?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow).ToOffset(TimeSpan.FromHours(7));
        var expiresAt = order.ActivationExpiresAt?.ToDateTimeOffset().ToOffset(TimeSpan.FromHours(7));
        var planName = order.Tier.Equals(SubscriptionTiers.Gold, StringComparison.OrdinalIgnoreCase) ? "Gold" : "Silver";
        var baseUrl = (configuration["App:PublicBaseUrl"] ?? configuration["RENDER_EXTERNAL_URL"] ?? "").TrimEnd('/');
        var subscriptionUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out _) ? $"{baseUrl}/Subscription" : "mục Gói của tôi trên HomePlant";

        return $"""
HOMEPLANT — BIÊN NHẬN THANH TOÁN

Thanh toán của bạn đã được xác nhận và gói đã được kích hoạt.

Mã đơn: {order.Id}
Nội dung chuyển khoản: {order.TransferReference}
Gói: {planName} · {order.DurationMonths} tháng
Số tiền: {order.AmountVnd:N0} VND
Thời gian thanh toán: {paidAt:dd/MM/yyyy HH:mm} (GMT+7)
Hiệu lực đến: {(expiresAt.HasValue ? expiresAt.Value.ToString("dd/MM/yyyy HH:mm") + " (GMT+7)" : "Xem trong tài khoản")}
Trạng thái: Đã thanh toán · Đã kích hoạt

Xem gói của bạn: {subscriptionUrl}

Hãy lưu email này và ảnh chụp giao dịch ngân hàng để phục vụ tra soát khi cần.
Nếu cần hỗ trợ, trả lời email này hoặc liên hệ lbienvda@gmail.com và cung cấp mã đơn ở trên.

Đây là biên nhận thanh toán điện tử của HomePlant, không phải hóa đơn giá trị gia tăng (VAT).
Không cung cấp mật khẩu, mã PIN hoặc OTP cho bất kỳ ai.
""";
    }

    private static Task Mark(DocumentReference reference, string status, string errorCode, DateTimeOffset attemptedAt, CancellationToken cancellationToken) =>
        reference.SetAsync(new Dictionary<string, object>
        {
            ["status"] = status,
            ["errorCode"] = errorCode,
            ["attemptedAt"] = Timestamp.FromDateTimeOffset(attemptedAt),
            [status == "Sent" ? "sentAt" : "failedAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        }, SetOptions.MergeAll, cancellationToken);
}
