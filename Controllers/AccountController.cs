using FirebaseAdmin.Auth;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class AccountController : Controller
{
    private readonly FirebaseAuthService _authService;

    public AccountController(
        FirebaseAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        RegisterVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            await _authService.Register(vm);
        }
        catch (FirebaseAuthException)
        {
            ModelState.AddModelError(
                "",
                "Không thể tạo tài khoản. Email này có thể đã được sử dụng.");

            return View(vm);
        }

        return View("RegisterPending", vm.Email);
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(
        string uid)
    {
        if (string.IsNullOrEmpty(uid))
            return RedirectToAction(nameof(Login));

        var verified = await _authService.CompleteVerification(uid);

        ViewBag.Success = verified;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(
        LoginVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result =
            await _authService
            .SignInWithPassword(vm.Email, vm.Password);

        if (result.IsLocked)
        {
            ModelState.AddModelError(
                "",
                "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");

            return View(vm);
        }

        if (result.EmailNotVerified)
        {
            ModelState.AddModelError(
                "",
                "Vui lòng xác thực email trước khi đăng nhập. Kiểm tra hộp thư của bạn.");

            return View(vm);
        }

        if (!result.Success || result.User == null)
        {
            ModelState.AddModelError(
                "",
                "Sai tài khoản hoặc mật khẩu");

            return View(vm);
        }

        SetSession(result.User);

        return RedirectToAction(
            "Index",
            "Home");
    }

    private void SetSession(UserModel user)
    {
        HttpContext.Session.SetString(
            "Uid",
            user.Id);

        HttpContext.Session.SetString(
            "Email",
            user.Email);

        HttpContext.Session.SetString(
            "Role",
            user.Role ?? "user");

        HttpContext.Session.SetString(
            "FullName",
            user.FullName ?? "");

        HttpContext.Session.SetString(
            "Phone",
            user.Phone ?? "");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction(
            "Index",
            "Home");
    }
}