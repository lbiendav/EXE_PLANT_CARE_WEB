using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class PlansController(
    PlanCatalogService catalog,
    PlanSettingsService planSettings,
    EntitlementService entitlements,
    PaymentModePolicy paymentPolicy) : Controller
{
    [HttpGet("/Plans")]
    public async Task<IActionResult> Index(string? sku = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        var current = uid == null ? null : await entitlements.Get(uid);
        var checkout = uid == null
            ? paymentPolicy.CanOfferCheckout()
            : paymentPolicy.CanCreateCheckout(uid);
        return View(new PlansVM
        {
            Plans = catalog.GetAll(),
            CurrentTier = current?.Tier,
            CurrentExpiresAt = current?.IsPaidActive == true ? current.Subscription?.ExpiresAt.ToDateTimeOffset() : null,
            SelectedSku = catalog.Find(sku)?.Sku,
            IsSignedIn = uid != null,
            CheckoutAvailable = checkout.Allowed,
            CheckoutUnavailableMessage = checkout.Message,
            PlanSettings = await planSettings.GetAll()
        });
    }
}
