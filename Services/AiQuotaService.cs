using Google.Cloud.Firestore;
using HomePlant.Models;

namespace HomePlant.Services;

public sealed class AiQuotaService(
    FirestoreService firestore,
    IConfiguration configuration,
    ISubscriptionClock clock,
    PaymentModePolicy paymentPolicy)
{
    private readonly FirestoreDb _db = firestore.Db;

    public bool IsEnforced => configuration.GetValue<bool?>("Subscriptions:EnforceLimits") ?? false;

    public async Task Reserve(string uid, string requestId)
    {
        if (!IsEnforced) return;
        if (!Guid.TryParse(requestId, out _)) throw new AiQuotaException("Yêu cầu phân tích không hợp lệ.");
        var now = clock.UtcNow;
        var month = SubscriptionTime.UsageMonth(now);
        var usageRef = _db.Collection("users").Document(uid).Collection("usage").Document(month);
        var stateRef = _db.Collection("users").Document(uid).Collection("usage_state").Document("current");
        var requestRef = _db.Collection("users").Document(uid).Collection("ai_requests").Document(requestId);
        var subscriptionRef = _db.Collection("subscriptions").Document(uid);
        await _db.RunTransactionAsync(async transaction =>
        {
            var usageSnapshot = await transaction.GetSnapshotAsync(usageRef);
            var stateSnapshot = await transaction.GetSnapshotAsync(stateRef);
            var requestSnapshot = await transaction.GetSnapshotAsync(requestRef);
            var subscriptionSnapshot = await transaction.GetSnapshotAsync(subscriptionRef);
            if (requestSnapshot.Exists)
            {
                var status = requestSnapshot.GetValue<string>("status");
                if (status == "completed") return;
                throw new AiQuotaException("Yêu cầu phân tích này đang được xử lý.");
            }
            var usage = usageSnapshot.Exists ? usageSnapshot.ConvertTo<UsageMonthModel>() : new UsageMonthModel();
            DocumentSnapshot? staleRequest = null;
            DocumentSnapshot? staleUsage = null;
            if (stateSnapshot.Exists && stateSnapshot.TryGetValue<string>("activeAiRequestId", out var active) && !string.IsNullOrWhiteSpace(active))
            {
                if (!stateSnapshot.TryGetValue<Timestamp>("leaseExpiresAt", out var leaseExpiry) || now < leaseExpiry.ToDateTimeOffset())
                    throw new AiQuotaException("Bạn đang có một phân tích khác đang chạy.");
                staleRequest = await transaction.GetSnapshotAsync(_db.Collection("users").Document(uid).Collection("ai_requests").Document(active));
                if (staleRequest.Exists && staleRequest.GetValue<string>("status") == "reserved")
                {
                    var staleMonth = staleRequest.GetValue<string>("month");
                    staleUsage = staleMonth == month
                        ? usageSnapshot
                        : await transaction.GetSnapshotAsync(_db.Collection("users").Document(uid).Collection("usage").Document(staleMonth));
                }
            }
            var limit = EffectiveAiLimit(subscriptionSnapshot, now);
            if (staleRequest?.Exists == true && staleRequest.GetValue<string>("status") == "reserved" && staleUsage != null)
            {
                var staleMonth = staleRequest.GetValue<string>("month");
                var oldUsage = staleUsage.Exists ? staleUsage.ConvertTo<UsageMonthModel>() : new UsageMonthModel();
                if (staleMonth == month)
                    usage.AiReserved = Math.Max(0, usage.AiReserved - 1);
                else
                    transaction.Set(staleUsage.Reference, new UsageMonthModel { AiUsed = oldUsage.AiUsed, AiReserved = Math.Max(0, oldUsage.AiReserved - 1), UpdatedAt = Timestamp.FromDateTimeOffset(now) });
                transaction.Update(staleRequest.Reference, new Dictionary<string, object> { ["status"] = "released", ["releasedAt"] = Timestamp.FromDateTimeOffset(now), ["releaseReason"] = "lease_expired" });
            }
            if (usage.AiUsed + usage.AiReserved >= limit)
                throw new AiQuotaException($"Bạn đã dùng hết {limit} lượt AI của tháng này.");
            var lease = Timestamp.FromDateTimeOffset(now.AddMinutes(3));
            transaction.Set(usageRef, new UsageMonthModel { AiUsed = usage.AiUsed, AiReserved = usage.AiReserved + 1, UpdatedAt = Timestamp.FromDateTimeOffset(now) });
            transaction.Set(stateRef, new { activeAiRequestId = requestId, activeAiMonth = month, leaseExpiresAt = lease, updatedAt = Timestamp.FromDateTimeOffset(now) }, SetOptions.MergeAll);
            transaction.Set(requestRef, new { month, status = "reserved", createdAt = Timestamp.FromDateTimeOffset(now), leaseExpiresAt = lease });
        });
    }

    public async Task<string> Complete(string uid, string requestId, AiDiagnosisModel diagnosis)
    {
        if (!IsEnforced)
        {
            var document = _db.Collection("ai_diagnoses").Document();
            diagnosis.DiagnosisId = document.Id;
            await document.SetAsync(diagnosis);
            return document.Id;
        }
        var requestRef = _db.Collection("users").Document(uid).Collection("ai_requests").Document(requestId);
        var diagnosisRef = _db.Collection("ai_diagnoses").Document(requestId);
        var stateRef = _db.Collection("users").Document(uid).Collection("usage_state").Document("current");
        diagnosis.DiagnosisId = diagnosisRef.Id;
        return await _db.RunTransactionAsync(async transaction =>
        {
            var request = await transaction.GetSnapshotAsync(requestRef);
            var state = await transaction.GetSnapshotAsync(stateRef);
            if (!request.Exists) throw new AiQuotaException("Không tìm thấy lượt AI đã giữ chỗ.");
            var status = request.GetValue<string>("status");
            if (status == "completed") return request.GetValue<string>("diagnosisId");
            if (status != "reserved") throw new AiQuotaException("Lượt AI không còn hiệu lực.");
            var month = request.GetValue<string>("month");
            var usageRef = _db.Collection("users").Document(uid).Collection("usage").Document(month);
            var usageSnapshot = await transaction.GetSnapshotAsync(usageRef);
            var usage = usageSnapshot.ConvertTo<UsageMonthModel>();
            transaction.Set(diagnosisRef, diagnosis);
            transaction.Update(usageRef, new Dictionary<string, object> { ["aiReserved"] = Math.Max(0, usage.AiReserved - 1), ["aiUsed"] = usage.AiUsed + 1, ["updatedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
            transaction.Update(requestRef, new Dictionary<string, object> { ["status"] = "completed", ["diagnosisId"] = diagnosisRef.Id, ["completedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
            if (state.Exists && state.TryGetValue<string>("activeAiRequestId", out var active) && active == requestId)
                transaction.Update(stateRef, new Dictionary<string, object> { ["activeAiRequestId"] = "", ["activeAiMonth"] = "", ["updatedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
            return diagnosisRef.Id;
        });
    }

    public async Task Release(string uid, string requestId)
    {
        if (!IsEnforced) return;
        var requestRef = _db.Collection("users").Document(uid).Collection("ai_requests").Document(requestId);
        var stateRef = _db.Collection("users").Document(uid).Collection("usage_state").Document("current");
        await _db.RunTransactionAsync(async transaction =>
        {
            var request = await transaction.GetSnapshotAsync(requestRef);
            var state = await transaction.GetSnapshotAsync(stateRef);
            if (!request.Exists || request.GetValue<string>("status") != "reserved") return;
            var month = request.GetValue<string>("month");
            var usageRef = _db.Collection("users").Document(uid).Collection("usage").Document(month);
            var usageSnapshot = await transaction.GetSnapshotAsync(usageRef);
            var usage = usageSnapshot.Exists ? usageSnapshot.ConvertTo<UsageMonthModel>() : new UsageMonthModel();
            transaction.Set(usageRef, new UsageMonthModel { AiUsed = usage.AiUsed, AiReserved = Math.Max(0, usage.AiReserved - 1), UpdatedAt = Timestamp.FromDateTimeOffset(clock.UtcNow) });
            transaction.Update(requestRef, new Dictionary<string, object> { ["status"] = "released", ["releasedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
            if (state.Exists && state.TryGetValue<string>("activeAiRequestId", out var active) && active == requestId)
                transaction.Update(stateRef, new Dictionary<string, object> { ["activeAiRequestId"] = "", ["activeAiMonth"] = "", ["updatedAt"] = Timestamp.FromDateTimeOffset(clock.UtcNow) });
        });
    }

    private int EffectiveAiLimit(DocumentSnapshot snapshot, DateTimeOffset now)
    {
        if (!snapshot.Exists) return PlanCatalogService.Free.MonthlyAiLimit;
        var subscription = snapshot.ConvertTo<SubscriptionModel>();
        var allowed = paymentPolicy.IsDemoSubscriptionAllowed(subscription);
        return allowed && subscription.StartsAt.ToDateTimeOffset() <= now && now < subscription.ExpiresAt.ToDateTimeOffset()
            ? subscription.MonthlyAiLimit : PlanCatalogService.Free.MonthlyAiLimit;
    }
}

public sealed class AiQuotaException(string message) : Exception(message);
