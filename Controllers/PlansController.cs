using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class PlansController(
    PlanCatalogService catalog,
    PlanSettingsService planSettings,
    EntitlementService entitlements,
    PaymentModePolicy paymentPolicy,
    ISubscriptionClock clock) : Controller
{
    [HttpGet("/Plans")]
    public async Task<IActionResult> Index(string? sku = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        var current = uid == null ? null : await entitlements.Get(uid);
        var checkout = uid == null
            ? paymentPolicy.CanOfferCheckout()
            : paymentPolicy.CanCreateCheckout(uid);
        var upgradeQuotes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (current?.IsPaidActive == true && current.Subscription != null &&
            current.Tier.Equals(SubscriptionTiers.Silver, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var target in catalog.GetAll().Where(plan => plan.Tier == SubscriptionTiers.Gold))
            {
                try { upgradeQuotes[target.Sku] = SubscriptionUpgradePolicy.Quote(current.Subscription, target, catalog, clock.UtcNow).AmountVnd; }
                catch (SubscriptionDomainException) { }
            }
        }
        return View(new PlansVM
        {
            Plans = catalog.GetAll(),
            CurrentTier = current?.Tier,
            CurrentExpiresAt = current?.IsPaidActive == true ? current.Subscription?.ExpiresAt.ToDateTimeOffset() : null,
            CurrentDurationMonths = current?.IsPaidActive == true ? current.Subscription?.DurationMonths : null,
            SelectedSku = catalog.Find(sku)?.Sku,
            IsSignedIn = uid != null,
            CheckoutAvailable = checkout.Allowed,
            CheckoutUnavailableMessage = checkout.Message,
            PlanSettings = await planSettings.GetAll(),
            UpgradeQuotes = upgradeQuotes
        });
    }
}
