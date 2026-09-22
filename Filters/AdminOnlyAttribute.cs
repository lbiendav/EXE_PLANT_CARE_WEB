using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomePlant.Filters;

public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("Role");

        if (role is not null && new[] { "admin", "super_admin", "content_admin" }.Contains(role, StringComparer.OrdinalIgnoreCase))
            return;

        if (context.HttpContext.Session.GetString("Uid") == null)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }
        else
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
