using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomePlant.Filters;

public sealed class RevenueOnlyAttribute : ActionFilterAttribute
{
    private static readonly HashSet<string> Roles = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "super_admin", "finance", "support" };

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (Roles.Contains(session.GetString("Role") ?? "")) return;
        context.Result = session.GetString("Uid") == null
            ? new RedirectToActionResult("Login", "Account", null)
            : new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
