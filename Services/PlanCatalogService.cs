using HomePlant.Models;

namespace HomePlant.Services;

public sealed class PlanCatalogService
{
    public const string Version = "2026-09-23";

    private static readonly IReadOnlyDictionary<string, PlanDefinition> Plans =
        new[]
        {
            new PlanDefinition("silver_1m", SubscriptionTiers.Silver, 1, 48_000, 10, 30, 48_000),
            new PlanDefinition("silver_6m", SubscriptionTiers.Silver, 6, 259_000, 10, 30, 48_000),
            new PlanDefinition("silver_12m", SubscriptionTiers.Silver, 12, 489_000, 10, 30, 48_000),
            // A plant limit of 0 represents an unlimited garden.
            new PlanDefinition("gold_1m", SubscriptionTiers.Gold, 1, 88_000, 0, 100, 88_000),
            new PlanDefinition("gold_6m", SubscriptionTiers.Gold, 6, 475_000, 0, 100, 88_000),
            new PlanDefinition("gold_12m", SubscriptionTiers.Gold, 12, 899_000, 0, 100, 88_000)
        }.ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

    public static EntitlementSnapshot Free => new(SubscriptionTiers.Free, 3, 3, Version);
    public IReadOnlyCollection<PlanDefinition> GetAll() => Plans.Values.ToArray();
    public PlanDefinition? Find(string? sku) => sku != null && Plans.TryGetValue(sku, out var plan) ? plan : null;
    public PlanDefinition Get(string sku) => Find(sku) ?? throw new SubscriptionDomainException("invalid_sku", "Gói đã chọn không hợp lệ.");
    public EntitlementSnapshot Snapshot(PlanDefinition plan) => new(plan.Tier, plan.PlantLimit, plan.MonthlyAiLimit, Version);
    public EntitlementSnapshot CurrentTier(string tier) => tier.Equals(SubscriptionTiers.Free, StringComparison.OrdinalIgnoreCase)
        ? Free
        : Snapshot(Plans.Values.First(x => x.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase)));
}
