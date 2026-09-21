using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed record PaymentIngestionResult(string Status, string? OrderId, bool Granted);

public sealed class LivePaymentService(
    FirestoreService firestore,
    ISubscriptionClock clock,
    IConfiguration configuration)
{
    private readonly FirestoreDb _db = firestore.Db;

    public async Task<PaymentIngestionResult> Ingest(VerifiedPayment payment)
    {
        var now = clock.UtcNow;
        var channelId = configuration["Payments:PayOS:ChannelId"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(channelId))
            throw new InvalidOperationException("Payment channel is not configured.");

        var transactionIdentity = string.IsNullOrWhiteSpace(payment.Reference)
            ? $"missing:{payment.OrderCode}:{payment.PaymentLinkId}:{payment.TransactionDateTime}"
            : payment.Reference;
        var financialKey = Hash($"payos:{channelId}:{transactionIdentity}");
        var receiptRef = _db.Collection("payment_webhook_receipts").Document(financialKey);
        var transactionRef = _db.Collection("payment_transactions").Document(financialKey);
        var mappingRef = _db.Collection("payment_provider_orders").Document(MappingId(channelId, payment.OrderCode));

        return await _db.RunTransactionAsync(async transaction =>
        {
            var receiptSnapshot = await transaction.GetSnapshotAsync(receiptRef);
            if (receiptSnapshot.Exists)
            {
                var oldStatus = receiptSnapshot.TryGetValue<string>("status", out var value) ? value : "Received";
                var oldOrderId = receiptSnapshot.TryGetValue<string>("orderId", out var valueOrderId) ? valueOrderId : null;
                return new PaymentIngestionResult(oldStatus, oldOrderId, oldStatus == "Granted");
            }

            var mappingSnapshot = await transaction.GetSnapshotAsync(mappingRef);
            if (!mappingSnapshot.Exists)
            {
                WriteEvidence(transaction, receiptRef, transactionRef, payment, now, null, "NeedsReview", "order_mapping_not_found");
                return new PaymentIngestionResult("NeedsReview", null, false);
            }

            var orderId = mappingSnapshot.GetValue<string>("orderId");
            var orderRef = _db.Collection("subscription_orders").Document(orderId);
            var orderSnapshot = await transaction.GetSnapshotAsync(orderRef);
            if (!orderSnapshot.Exists)
            {
                WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "NeedsReview", "order_not_found");
                return new PaymentIngestionResult("NeedsReview", orderId, false);
            }

            var order = orderSnapshot.ConvertTo<SubscriptionOrderModel>();
            var evidenceResult = ValidateEvidence(order, payment);
            if (evidenceResult != "ok")
            {
                WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "NeedsReview", evidenceResult);
                transaction.Update(orderRef, new Dictionary<string, object> { ["paymentStatus"] = "NeedsReview", ["fulfillmentStatus"] = "HeldForReview" });
                return new PaymentIngestionResult("NeedsReview", orderId, false);
            }

            var grantRef = _db.Collection("subscription_grants").Document(orderId);
            var grantSnapshot = await transaction.GetSnapshotAsync(grantRef);
            if (grantSnapshot.Exists)
            {
                WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "Granted", "duplicate_verified_webhook");
                return new PaymentIngestionResult("Granted", orderId, true);
            }

            var userRef = _db.Collection("users").Document(order.UserId);
            var subscriptionRef = _db.Collection("subscriptions").Document(order.UserId);
            var billingRef = userRef.Collection("billing_state").Document("live");
            var userSnapshot = await transaction.GetSnapshotAsync(userRef);
            var subscriptionSnapshot = await transaction.GetSnapshotAsync(subscriptionRef);
            var billingSnapshot = await transaction.GetSnapshotAsync(billingRef);
            if (!userSnapshot.Exists || (userSnapshot.TryGetValue<bool>("isLocked", out var locked) && locked))
            {
                WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "NeedsReview", "account_inactive");
                transaction.Update(orderRef, new Dictionary<string, object> { ["paymentStatus"] = "ReceivedExact", ["fulfillmentStatus"] = "HeldForReview" });
                return new PaymentIngestionResult("NeedsReview", orderId, false);
            }

            var baseTime = now;
            if (subscriptionSnapshot.Exists)
            {
                var current = subscriptionSnapshot.ConvertTo<SubscriptionModel>();
                var activeLive = !current.IsDemo && current.StartsAt.ToDateTimeOffset() <= now && now < current.ExpiresAt.ToDateTimeOffset();
                if (activeLive && !current.Tier.Equals(order.Tier, StringComparison.OrdinalIgnoreCase))
                {
                    WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "NeedsReview", "tier_conflict");
                    transaction.Update(orderRef, new Dictionary<string, object> { ["paymentStatus"] = "ReceivedExact", ["fulfillmentStatus"] = "HeldForReview" });
                    return new PaymentIngestionResult("NeedsReview", orderId, false);
                }
                if (activeLive) baseTime = current.ExpiresAt.ToDateTimeOffset();
            }

            var expiresAt = SubscriptionTime.AddCalendarMonths(baseTime, order.DurationMonths);
            var paidAt = Timestamp.FromDateTimeOffset(now);
            var subscription = new SubscriptionModel
            {
                UserId = order.UserId,
                Tier = order.Tier,
                StartsAt = paidAt,
                ExpiresAt = Timestamp.FromDateTimeOffset(expiresAt),
                DurationMonths = order.DurationMonths,
                CatalogVersion = order.CatalogVersion,
                PlantLimit = order.PlantLimit,
                MonthlyAiLimit = order.MonthlyAiLimit,
                LastOrderId = order.Id,
                IsDemo = false,
                UpdatedAt = paidAt,
                SchemaVersion = 2
            };

            WriteEvidence(transaction, receiptRef, transactionRef, payment, now, orderId, "Granted", "verified_exact_payment");
            transaction.Set(grantRef, new
            {
                orderId, userId = order.UserId, transactionId = financialKey, tier = order.Tier,
                startsAt = paidAt, expiresAt = subscription.ExpiresAt, status = "Granted", grantedAt = paidAt, schemaVersion = 1
            });
            transaction.Set(subscriptionRef, subscription);
            transaction.Update(orderRef, new Dictionary<string, object>
            {
                ["status"] = "Paid", ["checkoutStatus"] = "Closed", ["paymentStatus"] = "ReceivedExact",
                ["fulfillmentStatus"] = "Granted", ["paidAt"] = paidAt,
                ["activationStartsAt"] = paidAt, ["activationExpiresAt"] = subscription.ExpiresAt
            });
            if (billingSnapshot.Exists && billingSnapshot.TryGetValue<string>("pendingOrderId", out var pendingId) && pendingId == orderId)
                transaction.Update(billingRef, new Dictionary<string, object> { ["pendingOrderId"] = "", ["updatedAt"] = paidAt });
            return new PaymentIngestionResult("Granted", orderId, true);
        });
    }

    private static void WriteEvidence(
        Transaction transaction,
        DocumentReference receiptRef,
        DocumentReference transactionRef,
        VerifiedPayment payment,
        DateTimeOffset now,
        string? orderId,
        string status,
        string reason)
    {
        var timestamp = Timestamp.FromDateTimeOffset(now);
        var evidence = new Dictionary<string, object?>
        {
            ["provider"] = "PayOS", ["orderId"] = orderId, ["providerOrderCode"] = payment.OrderCode,
            ["paymentLinkId"] = payment.PaymentLinkId, ["reference"] = payment.Reference,
            ["amountVnd"] = payment.Amount, ["currency"] = payment.Currency, ["description"] = payment.Description,
            ["providerOccurredAt"] = payment.TransactionDateTime, ["receivedAt"] = timestamp,
            ["status"] = status, ["reason"] = reason, ["schemaVersion"] = 1
        };
        transaction.Set(receiptRef, evidence);
        transaction.Set(transactionRef, evidence);
    }

    public static string MappingId(string channelId, long orderCode) => Hash($"{channelId}:{orderCode}");
    public static string ValidateEvidence(SubscriptionOrderModel order, VerifiedPayment payment)
    {
        if (order.IsDemo || order.PaymentMode != "Live" || order.Provider != "PayOS") return "order_not_live_payos";
        if (order.ProviderOrderCode != payment.OrderCode) return "provider_order_code_mismatch";
        if (!string.Equals(order.ProviderPaymentLinkId, payment.PaymentLinkId, StringComparison.Ordinal)) return "payment_link_mismatch";
        if (payment.Amount != order.AmountVnd) return "amount_mismatch";
        if (!string.Equals(payment.Currency, "VND", StringComparison.OrdinalIgnoreCase)) return "currency_mismatch";
        if (!string.Equals(payment.ProviderCode, "00", StringComparison.Ordinal)) return "provider_code_not_success";
        if (string.IsNullOrWhiteSpace(payment.Reference)) return "reference_missing";
        return "ok";
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
