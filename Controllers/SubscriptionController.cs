using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class SubscriptionController(
    EntitlementService entitlements,
    UsageService usage,
    SubscriptionOrderService orders,
    ILogger<SubscriptionController> logger) : Controller
{
    [HttpGet("/Subscription")]
    public async Task<IActionResult> Index(int page = 1)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Subscription" });
        page = Math.Max(1, page);
        try
        {
            var currentTask = entitlements.Get(uid);
            var usageTask = usage.Get(uid);
            await Task.WhenAll(currentTask, usageTask);

            IReadOnlyList<HomePlant.Models.SubscriptionOrderModel> history = [];
            try
            {
                history = await orders.History(uid, page);
            }
            catch (Exception ex)
            {
                // Order history is useful, but it must not hide an otherwise valid entitlement.
                logger.LogError(ex, "Unable to load subscription order history for user {UserId}.", uid);
                ViewBag.HistoryLoadError = true;
            }

            return View(new SubscriptionVM { Current = currentTask.Result, Usage = usageTask.Result, Orders = history, Page = page });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load subscription overview for user {UserId}.", uid);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewBag.LoadError = true;
            return View();
        }
    }
}
