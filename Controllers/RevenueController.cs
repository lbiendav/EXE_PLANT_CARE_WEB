using HomePlant.Filters;
using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

[RevenueOnly]
[Route("Admin/Revenue")]
public sealed class RevenueController(RevenueAdminService revenue, ILogger<RevenueController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string tab = "overview", string query = "", string status = "", string detail = "") =>
        View(await revenue.Dashboard(tab, query ?? "", status ?? "", detail ?? ""));

    [HttpGet("Orders/{id}")]
    public async Task<IActionResult> Order(string id)
    {
        var model = await revenue.OrderDetail(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost("Orders/{id}/Cancel")]
    public Task<IActionResult> CancelOrder(string id, string reason) => Execute(async () => { RequireRole("admin", "super_admin", "finance"); await revenue.CancelOrder(id, Uid, Email, reason); }, "Đã hủy đơn và ghi nhật ký.", "orders");

    [HttpPost("Orders/{id}/Sync")]
    public Task<IActionResult> SyncOrder(string id, string reason) => Execute(async () => { RequireRole("admin", "super_admin", "finance"); await revenue.SyncOrder(id, Uid, Email, reason); }, "Đã đồng bộ trạng thái từ payOS.", "orders");

    [HttpPost("Orders/{id}/ResendReceipt")]
    public Task<IActionResult> ResendReceipt(string id, string reason) => Execute(async () => { RequireRole("admin", "super_admin", "finance", "support"); await revenue.ResendReceipt(id, Uid, Email, reason, HttpContext.RequestAborted); }, "Đã gửi lại email xác nhận.", "orders");

    [HttpPost("Subscriptions/Grant")]
    public Task<IActionResult> Grant(string userId, string sku, int months, string reason) => Execute(async () => { RequireRole("admin", "super_admin"); await revenue.Grant(userId, sku, months, Uid, Email, reason); }, "Đã cấp/gia hạn thuê bao.", "subscriptions");

    [HttpPost("Subscriptions/{userId}/Revoke")]
    public Task<IActionResult> Revoke(string userId, string reason) => Execute(async () => { RequireRole("admin", "super_admin"); await revenue.Revoke(userId, Uid, Email, reason); }, "Đã thu hồi thuê bao.", "subscriptions");

    [HttpPost("Plans/{sku}")]
    public Task<IActionResult> UpdatePlan(string sku, bool enabled, string description, string benefits, string reason) => Execute(async () => { RequireRole("admin", "super_admin"); await revenue.UpdatePlan(sku, enabled, description, benefits, Uid, Email, reason); }, "Đã cập nhật cấu hình gói.", "plans");

    [HttpPost("Customers/{userId}/Notes")]
    public Task<IActionResult> AddNote(string userId, string note) => Execute(() => revenue.AddNote(userId, Uid, Email, note), "Đã thêm ghi chú nội bộ.", "customers");

    [HttpPost("Customers/{userId}/AiUsage")]
    public Task<IActionResult> SetAiUsage(string userId, int aiUsed, string reason) => Execute(async () =>
    {
        RequireRole("admin", "super_admin");
        await revenue.SetAiUsage(userId, aiUsed, Uid, Email, reason);
    }, "Đã cập nhật số lượt AI đã sử dụng trong tháng.", "customers");

    private string Uid => HttpContext.Session.GetString("Uid")!;
    private string Email => HttpContext.Session.GetString("Email") ?? Uid;
    private void RequireRole(params string[] roles)
    {
        if (!roles.Contains(HttpContext.Session.GetString("Role") ?? "", StringComparer.OrdinalIgnoreCase))
            throw new SubscriptionDomainException("forbidden", "Tài khoản của bạn không có quyền thực hiện thao tác này.");
    }
    private async Task<IActionResult> Execute(Func<Task> operation, string success, string tab)
    {
        try { await operation(); TempData["Success"] = success; }
        catch (SubscriptionDomainException ex) { TempData["Warning"] = ex.Message; }
        catch (Exception ex) { logger.LogError(ex, "Revenue admin operation failed."); TempData["Warning"] = "Thao tác chưa hoàn tất. Dữ liệu chưa được xác nhận thay đổi; vui lòng kiểm tra lại."; }
        return RedirectToAction(nameof(Index), new { tab });
    }
}
