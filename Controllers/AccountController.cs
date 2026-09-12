using FirebaseAdmin.Auth;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        RegisterVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            await _authService.Register(vm);
        }
        catch (RegistrationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(vm);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError("", RegistrationEmailClient.RetryMessage);
            return View(vm);
        }
        catch (OperationCanceledException)
        {
            ModelState.AddModelError("", RegistrationEmailClient.RetryMessage);
            return View(vm);
        }
        catch (FirebaseAuthException)
        {
            ModelState.AddModelError(
                "",
                "Chưa thể hoàn tất đăng ký. Vui lòng thử lại sau bằng cùng email và mật khẩu, hoặc chọn Quên mật khẩu.");

            return View(vm);
        }

        return View("RegisterPending", vm.Email);
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        await _authService.SendPasswordResetEmail(vm.Email);

        return View("ForgotPasswordSent", vm.Email);
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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        LoginVM vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result =
            await _authService
            .SignInWithPassword(vm.Email, vm.Password);

        if (result.AccountNotFound)
        {
            ModelState.AddModelError(
                "",
                "Sai tài khoản hoặc mật khẩu");

            return View(vm);
        }

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

        HttpContext.Session.SetString(
            "AvatarUrl",
            user.AvatarUrl ?? "");
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction(
            "Index",
            "Home");
    }
}
