using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class NotificationsController : Controller
{
    private readonly CareReminderService _reminders;

    public NotificationsController(CareReminderService reminders)
    {
        _reminders = reminders;
    }

    public async Task<IActionResult> Index()
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        await _reminders.SyncForUser(
            uid,
            HttpContext.Session.GetString("Email"),
            HttpContext.RequestAborted);
        ViewBag.EmailEnabled = await _reminders.GetEmailPreference(uid, HttpContext.RequestAborted);
        ViewBag.EmailAvailable = _reminders.EmailDeliveryAvailable;
        ViewBag.EmailAddress = HttpContext.Session.GetString("Email");
        return View(await _reminders.GetAll(uid, HttpContext.RequestAborted));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return Unauthorized();

        await _reminders.SyncForUser(
            uid,
            HttpContext.Session.GetString("Email"),
            HttpContext.RequestAborted);
        var notificationsTask = _reminders.GetAll(uid, HttpContext.RequestAborted);
        var unreadCountTask = _reminders.GetUnreadCount(uid, HttpContext.RequestAborted);
        await Task.WhenAll(notificationsTask, unreadCountTask);
        var notifications = notificationsTask.Result;
        var latest = notifications.FirstOrDefault(notification => !notification.IsRead);

        return Json(new
        {
            count = unreadCountTask.Result,
            latest = latest == null ? null : new
            {
                latest.Id,
                latest.Title,
                latest.Message
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Preferences(bool emailEnabled)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        if (emailEnabled && !_reminders.EmailDeliveryAvailable)
        {
            TempData["Warning"] = "Email chưa sẵn sàng. Vui lòng thử lại sau.";
            return RedirectToAction(nameof(Index));
        }

        await _reminders.SetEmailPreference(uid, emailEnabled, HttpContext.RequestAborted);
        TempData["Success"] = emailEnabled
            ? "Đã bật nhắc việc qua email."
            : "Đã tắt nhắc việc qua email.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TestEmail()
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        TempData["Warning"] = await _reminders.SendTestEmail(uid, HttpContext.RequestAborted);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(string id, string? returnUrl = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        await _reminders.MarkRead(uid, id, HttpContext.RequestAborted);
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        await _reminders.MarkAllRead(uid, HttpContext.RequestAborted);
        return RedirectToAction(nameof(Index));
    }
}
