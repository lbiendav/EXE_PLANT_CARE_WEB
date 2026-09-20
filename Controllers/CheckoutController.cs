using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class CheckoutController(
    SubscriptionOrderService orders,
    DemoPaymentService demoPayments,
    IBankQrService qrService,
    IConfiguration configuration) : Controller
{
    [HttpPost("/Checkout/Create")]
    public async Task<IActionResult> Create(string sku, string requestKey)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return RedirectToAction("Login", "Account", new { returnUrl = $"/Plans?sku={Uri.EscapeDataString(sku ?? "")}" });
        try
        {
            var order = await orders.Create(uid, sku, requestKey);
            return RedirectToAction(nameof(Index), new { orderId = order.Id });
        }
        catch (SubscriptionDomainException ex)
        {
            TempData["Warning"] = ex.Message;
            return RedirectToAction("Index", "Plans", new { sku });
        }
    }

    [HttpGet("/Checkout/{orderId}")]
    public async Task<IActionResult> Index(string orderId)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return RedirectToAction("Login", "Account", new { returnUrl = $"/Checkout/{Uri.EscapeDataString(orderId)}" });
        var order = await orders.GetOwned(uid, orderId);
        if (order == null) return NotFound();
        var stage = configuration["App:DeploymentStage"] ?? "Production";
        var canSimulate = !stage.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
            (configuration["Payments:Mode"] ?? "").Equals("Demo", StringComparison.OrdinalIgnoreCase) &&
            (configuration.GetValue<bool?>("Payments:DemoEnabled") ?? false);
        return View(new CheckoutVM { Order = order, Qr = qrService.Build(order), CanSimulate = canSimulate });
    }

    [HttpGet("/Checkout/{orderId}/Status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(string orderId)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return Unauthorized(new { code = "not_authenticated" });
        var order = await orders.GetOwned(uid, orderId);
        return order == null ? NotFound(new { code = "not_found" }) : Ok(new { order.Status, expiresAt = order.ExpiresAt.ToDateTimeOffset(), order.PaidAt });
    }

    [HttpPost("/Checkout/{orderId}/SimulateSuccess")]
    public async Task<IActionResult> SimulateSuccess(string orderId)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return Unauthorized(new { code = "not_authenticated" });
        try
        {
            var order = await demoPayments.Confirm(uid, orderId);
            TempData["Success"] = $"Đã kích hoạt gói {order.Tier} (DEMO).";
            return RedirectToAction("Index", "Subscription");
        }
        catch (SubscriptionDomainException ex)
        {
            TempData["Warning"] = ex.Message;
            return RedirectToAction(nameof(Index), new { orderId });
        }
    }

    [HttpPost("/Checkout/{orderId}/Cancel")]
    public async Task<IActionResult> Cancel(string orderId)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return Unauthorized(new { code = "not_authenticated" });
        try { await orders.Cancel(uid, orderId); TempData["Success"] = "Đã hủy đơn."; }
        catch (SubscriptionDomainException ex) { TempData["Warning"] = ex.Message; }
        return RedirectToAction("Index", "Subscription");
    }
}
