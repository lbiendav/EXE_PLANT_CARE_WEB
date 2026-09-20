using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class SubscriptionController(
    EntitlementService entitlements,
    UsageService usage,
    SubscriptionOrderService orders) : Controller
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
            var historyTask = orders.History(uid, page);
            await Task.WhenAll(currentTask, usageTask, historyTask);
            return View(new SubscriptionVM { Current = currentTask.Result, Usage = usageTask.Result, Orders = historyTask.Result, Page = page });
        }
        catch
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewBag.LoadError = true;
            return View();
        }
    }
}
