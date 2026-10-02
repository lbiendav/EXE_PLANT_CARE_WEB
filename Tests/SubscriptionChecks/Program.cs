using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;

var catalog = new PlanCatalogService();
Check(catalog.GetAll().Count == 6, "catalog has six paid SKUs");
Check(catalog.Get("silver_1m").AmountVnd == 48_000, "Silver monthly price");
Check(catalog.Get("silver_6m").AmountVnd == 259_000, "Silver six-month price");
Check(catalog.Get("silver_12m").AmountVnd == 489_000, "Silver annual price");
Check(catalog.Get("gold_1m").AmountVnd == 88_000, "Gold monthly price");
Check(catalog.Get("gold_6m").AmountVnd == 475_000, "Gold six-month price");
Check(catalog.Get("gold_12m").AmountVnd == 899_000, "Gold annual price");
Check(catalog.Get("silver_1m").PlantLimit == 10, "Silver allows ten plants");
Check(catalog.Get("gold_1m").PlantLimit == 0, "Gold has unlimited plants");
Check(catalog.Get("silver_12m").MonthlyAiLimit == 30, "long duration does not multiply monthly AI quota");
Check(PlanCatalogService.Free.PlantLimit == 3 && PlanCatalogService.Free.MonthlyAiLimit == 3, "Basic entitlements");

var vietnamOffset = TimeSpan.FromHours(7);
var jan31 = new DateTimeOffset(2024, 1, 31, 10, 15, 0, vietnamOffset);
var leap = SubscriptionTime.AddCalendarMonths(jan31, 1).ToOffset(vietnamOffset);
Check(leap == new DateTimeOffset(2024, 2, 29, 10, 15, 0, vietnamOffset), "AddMonths clamps leap February");
var nonLeap = SubscriptionTime.AddCalendarMonths(new DateTimeOffset(2025, 1, 31, 10, 15, 0, vietnamOffset), 1).ToOffset(vietnamOffset);
Check(nonLeap.Day == 28 && nonLeap.Hour == 10, "AddMonths clamps non-leap February");
var beforeMidnightUtc = new DateTimeOffset(2026, 9, 30, 16, 59, 59, TimeSpan.Zero);
var afterMidnightUtc = beforeMidnightUtc.AddSeconds(1);
Check(SubscriptionTime.UsageMonth(beforeMidnightUtc) == "2026-09", "quota month before Vietnam midnight");
Check(SubscriptionTime.UsageMonth(afterMidnightUtc) == "2026-10", "quota month after Vietnam midnight");
Check(SubscriptionTime.NextUsageReset(beforeMidnightUtc) == afterMidnightUtc, "quota reset instant");

var upgradeNow = new DateTimeOffset(2026, 10, 2, 1, 0, 0, TimeSpan.Zero);
var silverStart = upgradeNow;
var silverExpiry = SubscriptionTime.AddCalendarMonths(silverStart, 1);
var silver = new SubscriptionModel
{
    Tier = SubscriptionTiers.Silver,
    StartsAt = Timestamp.FromDateTimeOffset(silverStart),
    ExpiresAt = Timestamp.FromDateTimeOffset(silverExpiry),
    DurationMonths = 1,
    LastOrderId = "silver-order"
};
var fullUpgrade = SubscriptionUpgradePolicy.Quote(silver, catalog.Get("gold_1m"), catalog, upgradeNow);
Check(fullUpgrade.AmountVnd == 40_000, "fresh Silver monthly upgrade charges exact 40k difference");
var halfUpgrade = SubscriptionUpgradePolicy.Quote(silver, catalog.Get("gold_1m"), catalog, upgradeNow + (silverExpiry - silverStart) / 2);
Check(halfUpgrade.AmountVnd == 20_000, "half-used Silver monthly upgrade prorates to 20k");
Check(fullUpgrade.ExpiresAt == silverExpiry, "upgrade preserves current expiry");
CheckThrows(() => SubscriptionUpgradePolicy.Quote(silver, catalog.Get("gold_6m"), catalog, upgradeNow), "upgrade requires matching duration");
var upgradeOrder = new SubscriptionOrderModel
{
    OrderKind = SubscriptionOrderKinds.Upgrade,
    UpgradeFromTier = SubscriptionTiers.Silver,
    UpgradeSourceOrderId = "silver-order",
    UpgradeSourceExpiresAt = silver.ExpiresAt
};
Check(SubscriptionUpgradePolicy.SourceStillMatches(silver, upgradeOrder, upgradeNow), "upgrade source snapshot matches unchanged subscription");
silver.LastOrderId = "renewed-order";
Check(!SubscriptionUpgradePolicy.SourceStillMatches(silver, upgradeOrder, upgradeNow), "upgrade source snapshot rejects concurrent subscription change");

Console.WriteLine("Subscription checks passed.");

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL {name}");
    Console.WriteLine($"PASS {name}");
}

static void CheckThrows(Action action, string name)
{
    try { action(); }
    catch (SubscriptionDomainException) { Console.WriteLine($"PASS {name}"); return; }
    throw new Exception($"FAIL {name}");
}
