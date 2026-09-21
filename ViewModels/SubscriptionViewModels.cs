using HomePlant.Models;
using HomePlant.Services;

namespace HomePlant.ViewModels;

public sealed class PlansVM
{
    public IReadOnlyCollection<PlanDefinition> Plans { get; init; } = [];
    public string? CurrentTier { get; init; }
    public DateTimeOffset? CurrentExpiresAt { get; init; }
    public string? SelectedSku { get; init; }
    public bool IsSignedIn { get; init; }
    public bool SubscriptionsEnabled { get; init; }
}

public sealed class SubscriptionVM
{
    public required CurrentEntitlement Current { get; init; }
    public required SubscriptionUsage Usage { get; init; }
    public IReadOnlyList<SubscriptionOrderModel> Orders { get; init; } = [];
    public int Page { get; init; }
}

public sealed class CheckoutVM
{
    public required SubscriptionOrderModel Order { get; init; }
    public required BankQrDetails Qr { get; init; }
    public bool CanSimulate { get; init; }
    public bool IsCheckoutOpen { get; init; }
}
