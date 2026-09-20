using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class PlansController(
    PlanCatalogService catalog,
    EntitlementService entitlements,
    IConfiguration configuration) : Controller
{
    [HttpGet("/Plans")]
    public async Task<IActionResult> Index(string? sku = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        var current = uid == null ? null : await entitlements.Get(uid);
        return View(new PlansVM
        {
            Plans = catalog.GetAll(),
            CurrentTier = current?.Tier,
            CurrentExpiresAt = current?.IsPaidActive == true ? current.Subscription?.ExpiresAt.ToDateTimeOffset() : null,
            SelectedSku = catalog.Find(sku)?.Sku,
            IsSignedIn = uid != null,
            SubscriptionsEnabled = configuration.GetValue<bool?>("Subscriptions:Enabled") ?? false
        });
    }
}
