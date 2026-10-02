using HomePlant.Models;

namespace HomePlant.Services;

public sealed record SubscriptionUpgradeQuote(
    string FromTier,
    string ToTier,
    int DurationMonths,
    long AmountVnd,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    string SourceOrderId);

public static class SubscriptionUpgradePolicy
{
    private const long RoundingUnitVnd = 1_000;

    public static SubscriptionUpgradeQuote Quote(
        SubscriptionModel current,
        PlanDefinition target,
        PlanCatalogService catalog,
        DateTimeOffset now)
    {
        var startsAt = current.StartsAt.ToDateTimeOffset();
        var expiresAt = current.ExpiresAt.ToDateTimeOffset();
        if (now < startsAt || now >= expiresAt)
            throw new SubscriptionDomainException("subscription_not_active", "Gói hiện tại không còn hiệu lực để nâng cấp.");
        if (!current.Tier.Equals(SubscriptionTiers.Silver, StringComparison.OrdinalIgnoreCase) ||
            !target.Tier.Equals(SubscriptionTiers.Gold, StringComparison.OrdinalIgnoreCase))
            throw new SubscriptionDomainException("tier_change_not_supported", "Hiện chỉ hỗ trợ nâng cấp từ Silver lên Gold khi gói còn hạn.");
        if (target.DurationMonths != current.DurationMonths)
            throw new SubscriptionDomainException("upgrade_duration_mismatch", $"Hãy chọn Gold kỳ hạn {current.DurationMonths} tháng để nâng cấp gói hiện tại.");
        if (string.IsNullOrWhiteSpace(current.LastOrderId))
            throw new SubscriptionDomainException("upgrade_source_unknown", "Gói hiện tại thiếu thông tin đơn nguồn và chưa thể nâng cấp tự động.");

        var source = catalog.Find(current.Tier, current.DurationMonths)
            ?? throw new SubscriptionDomainException("upgrade_source_unknown", "Không xác định được giá gốc của gói hiện tại.");
        var priceDifference = target.AmountVnd - source.AmountVnd;
        if (priceDifference <= 0)
            throw new SubscriptionDomainException("invalid_upgrade_price", "Giá nâng cấp hiện không hợp lệ.");

        var nominalEnd = SubscriptionTime.AddCalendarMonths(startsAt, current.DurationMonths);
        var nominalTicks = nominalEnd.UtcTicks - startsAt.UtcTicks;
        var remainingTicks = expiresAt.UtcTicks - now.UtcTicks;
        if (nominalTicks <= 0 || remainingTicks <= 0)
            throw new SubscriptionDomainException("subscription_not_active", "Gói hiện tại không còn hiệu lực để nâng cấp.");

        // Include previously purchased extensions in the remaining value instead of
        // silently converting them to Gold for free. Round up to a payable VND unit.
        var rawAmount = (decimal)priceDifference * remainingTicks / nominalTicks;
        var amount = checked((long)(Math.Ceiling(rawAmount / RoundingUnitVnd) * RoundingUnitVnd));
        return new SubscriptionUpgradeQuote(
            current.Tier, target.Tier, current.DurationMonths, Math.Max(RoundingUnitVnd, amount),
            now, expiresAt, current.LastOrderId);
    }

    public static bool SourceStillMatches(SubscriptionModel current, SubscriptionOrderModel order, DateTimeOffset now) =>
        order.IsUpgrade &&
        !string.IsNullOrWhiteSpace(order.UpgradeSourceOrderId) &&
        order.UpgradeSourceExpiresAt.HasValue &&
        current.Tier.Equals(order.UpgradeFromTier, StringComparison.OrdinalIgnoreCase) &&
        current.LastOrderId == order.UpgradeSourceOrderId &&
        current.ExpiresAt == order.UpgradeSourceExpiresAt.Value &&
        current.StartsAt.ToDateTimeOffset() <= now && now < current.ExpiresAt.ToDateTimeOffset();
}
