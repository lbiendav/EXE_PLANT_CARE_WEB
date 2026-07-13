using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomePlant.Filters;

public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("Role");

        if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
        }
    }
}
