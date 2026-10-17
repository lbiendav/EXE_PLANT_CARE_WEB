using HomePlant.Services;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

namespace HomePlant.Filters;

// Check current database permissions before controller-specific authorization.
// Sessions must not retain admin access or stay usable after an account is locked.
public sealed class ActiveUserFilter(
    FirebaseAuthService authService,
    UserActivityService activityService) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -1000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        var uid = session.GetString("Uid");
        if (uid != null)
        {
            UserSessionState state;
            try
            {
                state = await authService.GetUserSessionState(uid);
            }
            catch (FirebaseAuthException)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            var sessionCreatedAt = session.GetString("AuthSessionCreatedAt");
            var sessionStarted = DateTimeOffset.TryParse(sessionCreatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedSessionStarted)
                ? parsedSessionStarted
                : DateTimeOffset.MinValue;
            var revoked = state.TokensValidAfter is { } validAfter && validAfter > sessionStarted;
            if (state.User == null || state.AuthUserMissing || state.AuthDisabled || state.User.IsLocked || revoked)
            {
                session.Clear();
                var wantsJson = context.HttpContext.Request.Path.StartsWithSegments("/Checkout") &&
                    (context.HttpContext.Request.Path.Value?.EndsWith("/Status", StringComparison.OrdinalIgnoreCase) == true ||
                     context.HttpContext.Request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true));
                context.Result = wantsJson
                    ? new JsonResult(new { code = "account_inactive", message = "Tài khoản không còn hoạt động." }) { StatusCode = StatusCodes.Status403Forbidden }
                    : new RedirectToActionResult("Login", "Account", null);
                return;
            }
            session.SetString("Role", state.User.Role ?? "user");

            // Automatic activity and session tracking (chỉ theo dõi các trang user dùng, bỏ qua hoàn toàn các trang admin quản trị)
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            var action = context.RouteData.Values["action"]?.ToString() ?? "";
            var method = context.HttpContext.Request.Method;
            var path = context.HttpContext.Request.Path.Value ?? "/";

            var isAdminPage = string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(controller, "Revenue", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/Revenue", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/readyz", StringComparison.OrdinalIgnoreCase);

            if (!isAdminPage &&
                !string.Equals(action, "UnreadCount", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "UserActivities", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "DailyUsers", StringComparison.OrdinalIgnoreCase))
            {
                var isNewSession = false;
                if (session.GetString("UserSessionTracked") == null)
                {
                    session.SetString("UserSessionTracked", "1");
                    isNewSession = true;
                }

                var currentNavKey = $"{method}:{controller}/{action}";
                var lastNavKey = session.GetString("LastNavKey");
                if (lastNavKey != currentNavKey || isNewSession)
                {
                    session.SetString("LastNavKey", currentNavKey);
                    var (title, actionName) = ResolveActivity(controller, action, method);
                    var fullName = string.IsNullOrWhiteSpace(state.User.FullName) ? "Người dùng" : state.User.FullName;
                    var email = state.User.Email ?? "";
                    var avatarUrl = state.User.AvatarUrl ?? "";
                    var isNewUser = state.User.CreatedAt.AddHours(7).ToString("yyyyMMdd") == DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd");

                    _ = activityService.LogActivityAsync(uid, path, title, actionName, isNewSession);
                    _ = activityService.LogDailyActivityAsync(uid, fullName, email, avatarUrl, isGuest: false, isNew: isNewUser, path, title, actionName, isNewSession);
                }
            }
        }
        else
        {
            // Anonymous guest tracking
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            var action = context.RouteData.Values["action"]?.ToString() ?? "";
            var method = context.HttpContext.Request.Method;
            var path = context.HttpContext.Request.Path.Value ?? "/";

            var isAdminPage = string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(controller, "Revenue", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/Revenue", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith("/readyz", StringComparison.OrdinalIgnoreCase);

            if (!isAdminPage &&
                !string.Equals(action, "UnreadCount", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "UserActivities", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "DailyUsers", StringComparison.OrdinalIgnoreCase))
            {
                var guestId = session.GetString("GuestTrackingId");
                var isNewSession = false;
                if (string.IsNullOrEmpty(guestId))
                {
                    guestId = "guest_" + Guid.NewGuid().ToString("N")[..8];
                    session.SetString("GuestTrackingId", guestId);
                    isNewSession = true;
                }

                var currentNavKey = $"{method}:{controller}/{action}";
                var lastNavKey = session.GetString("LastNavKey");
                if (lastNavKey != currentNavKey || isNewSession)
                {
                    session.SetString("LastNavKey", currentNavKey);
                    var (title, actionName) = ResolveActivity(controller, action, method);
                    _ = activityService.LogDailyActivityAsync(
                        userId: guestId,
                        fullName: $"Khách vãng lai #{guestId[6..]}",
                        email: "Chưa đăng nhập (Khách)",
                        avatarUrl: "",
                        isGuest: true,
                        isNew: true,
                        path: path,
                        title: title,
                        action: actionName,
                        isNewSession: isNewSession
                    );
                }
            }
        }
        await next();
    }

    private static (string title, string actionName) ResolveActivity(string controller, string action, string method)
    {
        var key = $"{controller}/{action}".ToLowerInvariant();
        var isPost = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "home/index" => ("Trang chủ", "Xem trang"),
            "home/privacy" => ("Chính sách bảo mật", "Xem trang"),
            "home/terms" => ("Điều khoản dịch vụ", "Xem trang"),
            "home/support" => ("Hỗ trợ khách hàng", "Xem trang"),
            "library/index" => ("Thư viện cây", "Xem thư viện"),
            "library/details" => ("Chi tiết loài cây trong thư viện", "Xem chi tiết cây"),
            "plant/index" => ("Danh sách cây trong vườn", "Xem vườn cây"),
            "plant/details" => ("Chi tiết cây trong vườn", "Xem cây cá nhân"),
            "plant/create" => ("Thêm cây mới vào vườn", isPost ? "Lưu cây mới" : "Mở form thêm cây"),
            "plant/edit" => ("Chỉnh sửa thông tin cây", isPost ? "Lưu thông tin cây" : "Mở form sửa"),
            "carelog/index" => ("Nhật ký chăm sóc", "Xem nhật ký"),
            "carelog/create" => ("Ghi nhận nhật ký chăm sóc", isPost ? "Lưu nhật ký chăm sóc" : "Mở form ghi nhật ký"),
            "article/index" => ("Danh sách bài viết", "Xem bài viết"),
            "article/details" => ("Đọc bài viết chi tiết", "Đọc bài viết"),
            "article/create" => ("Đăng bài viết mới", isPost ? "Xuất bản bài viết" : "Mở form đăng"),
            "community/index" => ("Cộng đồng yêu cây HomePlant", "Lướt cộng đồng"),
            "expert/index" => ("Hỏi đáp cùng chuyên gia", "Hỏi đáp"),
            "expert/details" => ("Chi tiết câu hỏi chuyên gia", "Xem thảo luận"),
            "profile/index" => ("Hồ sơ cá nhân", "Xem hồ sơ"),
            "profile/edit" => ("Chỉnh sửa hồ sơ", isPost ? "Cập nhật hồ sơ" : "Mở form sửa"),
            "plans/index" => ("Bảng giá gói thành viên", "Xem gói dịch vụ"),
            "checkout/index" => ("Thanh toán gói dịch vụ", "Mở trang thanh toán"),
            "notifications/index" => ("Trung tâm thông báo", "Xem thông báo"),
            "account/login" => ("Đăng nhập", "Đăng nhập"),
            _ => ($"{controller} - {action}", isPost ? "Thực hiện thao tác" : "Xem trang")
        };
    }
}
