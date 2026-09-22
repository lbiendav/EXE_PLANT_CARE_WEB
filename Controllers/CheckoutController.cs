using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class CheckoutController(
    SubscriptionOrderService orders,
    DemoPaymentService demoPayments,
    PaymentModePolicy paymentPolicy) : Controller
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
        var isCheckoutOpen = order.Status == "Pending" && !order.IsCheckoutExpired(DateTimeOffset.UtcNow);
        var canSimulate = isCheckoutOpen && paymentPolicy.CanSimulate(order).Allowed;
        return View(new CheckoutVM
        {
            Order = order,
            // Staging demo orders must never expose a payable bank QR. A real bank
            // transfer cannot be verified by the simulator and could move real money.
            Qr = new BankQrDetails(false, null, order.IsDemo
                ? "Staging chỉ mô phỏng trạng thái thanh toán; không gửi giao dịch tới ngân hàng."
                : "Thanh toán thật sử dụng checkout do payOS cung cấp."),
            CanSimulate = canSimulate,
            IsCheckoutOpen = isCheckoutOpen
        });
    }

    [HttpGet("/Checkout/{orderId}/Status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(string orderId)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null) return Unauthorized(new { code = "not_authenticated" });
        var order = await orders.GetOwned(uid, orderId);
        if (order == null) return NotFound(new { code = "not_found" });
        var effectiveStatus = order.IsCheckoutExpired(DateTimeOffset.UtcNow) ? "Expired" : order.Status;
        return Ok(new
        {
            status = effectiveStatus,
            order.CheckoutStatus,
            order.PaymentStatus,
            order.FulfillmentStatus,
            expiresAt = order.ExpiresAt.ToDateTimeOffset(),
            order.PaidAt
        });
    }

    [HttpGet("/Checkout/{orderId}/Return")]
    public IActionResult Return(string orderId)
    {
        // Query parameters from the browser are not financial evidence. The webhook updates payment state.
        return RedirectToAction(nameof(Index), new { orderId });
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
