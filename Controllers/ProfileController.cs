using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class ProfileController : Controller
{
    private readonly FirebaseAuthService _authService;
    private readonly UserService _userService;
    private readonly ImgBbService _imgBbService;

    public ProfileController(
        FirebaseAuthService authService,
        UserService userService,
        ImgBbService imgBbService)
    {
        _authService = authService;
        _userService = userService;
        _imgBbService = imgBbService;
    }

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

        ViewBag.AvatarUrl =
            HttpContext.Session.GetString(
                "AvatarUrl");

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var user = await _authService.GetUser(uid);

        if (user == null)
            return NotFound();

        var vm = new ProfileVM
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        ProfileVM vm,
        IFormFile? avatar)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(vm.FullName))
        {
            ModelState.AddModelError(nameof(vm.FullName), "Vui lòng nhập họ tên");
            return View(vm);
        }

        var avatarUrl = avatar != null
            ? await _imgBbService.Upload(avatar)
            : vm.AvatarUrl;

        await _userService.UpdateProfile(
            uid,
            vm.FullName,
            vm.Phone,
            avatarUrl);

        HttpContext.Session.SetString("FullName", vm.FullName ?? "");
        HttpContext.Session.SetString("Phone", vm.Phone ?? "");
        HttpContext.Session.SetString("AvatarUrl", avatarUrl ?? "");

        TempData["Success"] = "Cập nhật hồ sơ thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
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

    [HttpPost]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");
        var email = HttpContext.Session.GetString("Email");

        if (uid == null || email == null)
            return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return View(vm);

        var changed = await _authService.ChangePassword(
            uid,
            email,
            vm.CurrentPassword,
            vm.NewPassword);

        if (!changed)
        {
            ModelState.AddModelError("", "Mật khẩu hiện tại không đúng.");
            return View(vm);
        }

        TempData["Success"] = "Đổi mật khẩu thành công.";

        return RedirectToAction(nameof(Index));
    }
}
