using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomePlant.Filters;

public sealed class PrivilegedAdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("admin", StringComparison.OrdinalIgnoreCase) || role.Equals("super_admin", StringComparison.OrdinalIgnoreCase)) return;
        context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
