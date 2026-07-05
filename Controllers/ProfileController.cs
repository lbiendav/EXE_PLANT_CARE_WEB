using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class ProfileController : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("Email") == null)
        {
            return RedirectToAction(
            "Login",
            "Account");
        }

    ViewBag.Name =
        HttpContext.Session.GetString(
            "FullName");

        ViewBag.Email =
            HttpContext.Session.GetString(
                "Email");

        ViewBag.Phone =
            HttpContext.Session.GetString(
                "Phone");

        ViewBag.Role =
            HttpContext.Session.GetString(
                "Role");

        return View();
    }

    public IActionResult ChangePassword()
    {
        if (HttpContext.Session.GetString("Email") == null)
        {
            return RedirectToAction(
                "Login",
                "Account");
        }

        return View();
    }

}
