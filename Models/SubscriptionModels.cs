using Google.Cloud.Firestore;

namespace HomePlant.Models;

public static class SubscriptionTiers
{
    public const string Free = "Free";
    public const string Silver = "Silver";
    public const string Gold = "Gold";
}

public sealed record PlanDefinition(
    string Sku,
    string Tier,
    int DurationMonths,
    long AmountVnd,
    int PlantLimit,
    int MonthlyAiLimit,
    long MonthlyListPriceVnd);

public sealed record EntitlementSnapshot(
    string Tier,
    int PlantLimit,
    int MonthlyAiLimit,
    string CatalogVersion);

public sealed record CurrentEntitlement(
    EntitlementSnapshot Entitlements,
    SubscriptionModel? Subscription,
    bool IsPaidActive)
{
    public string Tier => Entitlements.Tier;
    public int PlantLimit => Entitlements.PlantLimit;
    public int MonthlyAiLimit => Entitlements.MonthlyAiLimit;
}

[FirestoreData]
public sealed class SubscriptionModel
{
    [FirestoreDocumentId] public string UserId { get; set; } = "";
    [FirestoreProperty("tier")] public string Tier { get; set; } = SubscriptionTiers.Free;
    [FirestoreProperty("startsAt")] public Timestamp StartsAt { get; set; }
    [FirestoreProperty("expiresAt")] public Timestamp ExpiresAt { get; set; }
    [FirestoreProperty("durationMonths")] public int DurationMonths { get; set; }
    [FirestoreProperty("catalogVersion")] public string CatalogVersion { get; set; } = "2026-09";
    [FirestoreProperty("plantLimit")] public int PlantLimit { get; set; }
    [FirestoreProperty("monthlyAiLimit")] public int MonthlyAiLimit { get; set; }
    [FirestoreProperty("lastOrderId")] public string LastOrderId { get; set; } = "";
    [FirestoreProperty("isDemo")] public bool IsDemo { get; set; }
    [FirestoreProperty("updatedAt")] public Timestamp UpdatedAt { get; set; }
    [FirestoreProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
}

[FirestoreData]
public sealed class SubscriptionOrderModel
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty("userId")] public string UserId { get; set; } = "";
    [FirestoreProperty("sku")] public string Sku { get; set; } = "";
    [FirestoreProperty("tier")] public string Tier { get; set; } = "";
    [FirestoreProperty("durationMonths")] public int DurationMonths { get; set; }
    [FirestoreProperty("amountVnd")] public long AmountVnd { get; set; }
    [FirestoreProperty("currency")] public string Currency { get; set; } = "VND";
    [FirestoreProperty("catalogVersion")] public string CatalogVersion { get; set; } = "";
    [FirestoreProperty("plantLimit")] public int PlantLimit { get; set; }
    [FirestoreProperty("monthlyAiLimit")] public int MonthlyAiLimit { get; set; }
    [FirestoreProperty("status")] public string Status { get; set; } = "Pending";
    [FirestoreProperty("createdAt")] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty("expiresAt")] public Timestamp ExpiresAt { get; set; }
    [FirestoreProperty("paidAt")] public Timestamp? PaidAt { get; set; }
    [FirestoreProperty("paymentMode")] public string PaymentMode { get; set; } = "Demo";
    [FirestoreProperty("isDemo")] public bool IsDemo { get; set; } = true;
    [FirestoreProperty("transferReference")] public string TransferReference { get; set; } = "";
    [FirestoreProperty("bankBin")] public string BankBin { get; set; } = "";
    [FirestoreProperty("bankAccountNumber")] public string BankAccountNumber { get; set; } = "";
    [FirestoreProperty("bankAccountName")] public string BankAccountName { get; set; } = "";
    [FirestoreProperty("activationStartsAt")] public Timestamp? ActivationStartsAt { get; set; }
    [FirestoreProperty("activationExpiresAt")] public Timestamp? ActivationExpiresAt { get; set; }
}

[FirestoreData]
public sealed class UsageMonthModel
{
    [FirestoreProperty("aiUsed")] public int AiUsed { get; set; }
    [FirestoreProperty("aiReserved")] public int AiReserved { get; set; }
    [FirestoreProperty("updatedAt")] public Timestamp UpdatedAt { get; set; }
}

public sealed record SubscriptionUsage(int PlantCount, int AiUsed, int AiReserved, DateTimeOffset ResetsAt);

public sealed class SubscriptionDomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
