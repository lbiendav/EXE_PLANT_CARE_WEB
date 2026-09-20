using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomePlant.Filters;

// Check current database permissions before controller-specific authorization.
// Sessions must not retain admin access or stay usable after an account is locked.
public sealed class ActiveUserFilter(FirebaseAuthService authService) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -1000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        var uid = session.GetString("Uid");
        if (uid != null)
        {
            var user = await authService.GetUser(uid);
            if (user == null || user.IsLocked)
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
            session.SetString("Role", user.Role ?? "user");
        }
        await next();
    }
}
