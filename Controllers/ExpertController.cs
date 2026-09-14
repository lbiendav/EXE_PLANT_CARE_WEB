using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;

namespace HomePlant.Controllers;

public sealed class ExpertController : Controller
{
    private readonly UserPlantService _userPlantService;
    private readonly AiDiagnosisService _diagnosisService;
    private readonly PlantExpertAiService _expertAi;
    private readonly ImageStorageService _imageStorage;

    public ExpertController(
        UserPlantService userPlantService,
        AiDiagnosisService diagnosisService,
        PlantExpertAiService expertAi,
        ImageStorageService imageStorage)
    {
        _userPlantService = userPlantService;
        _diagnosisService = diagnosisService;
        _expertAi = expertAi;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? plantId = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        return View(await BuildIndexViewModel(uid, new ExpertIndexVM
        {
            PlantId = plantId ?? ""
        }));
    }

    [HttpPost]
    [EnableRateLimiting("ai")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Ask(ExpertIndexVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        vm.Question = vm.Question?.Trim() ?? "";
        var imageError = await PlantExpertAiService.ValidateImage(vm.Photo, HttpContext.RequestAborted);
        if (imageError != null)
            ModelState.AddModelError(nameof(vm.Photo), imageError);

        if (!vm.ConsentToAiProcessing)
        {
            ModelState.AddModelError(
                nameof(vm.ConsentToAiProcessing),
                "Bạn cần đồng ý gửi ảnh và câu hỏi tới Gemini để phân tích.");
        }

        UserPlantModel? plant = null;
        if (!string.IsNullOrWhiteSpace(vm.PlantId))
        {
            plant = await _userPlantService.GetById(uid, vm.PlantId);
            if (plant == null)
                ModelState.AddModelError(nameof(vm.PlantId), "Cây được chọn không tồn tại trong vườn của bạn.");
        }

        if (!_expertAi.IsConfigured)
            ModelState.AddModelError("", "Chưa cấu hình GEMINI_API_KEY cho máy chủ.");

        if (!ModelState.IsValid || vm.Photo == null)
            return View("Index", await BuildIndexViewModel(uid, vm));

        AiDiagnosisResultModel result;
        try
        {
            result = await _expertAi.Analyze(
                vm.Photo,
                vm.Question,
                plant,
                HttpContext.RequestAborted);
        }
        catch (PlantAiException exception)
        {
            ModelState.AddModelError("", exception.Message);
            return View("Index", await BuildIndexViewModel(uid, vm));
        }

        var imageUrl = await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted);
        var diagnosis = new AiDiagnosisModel
        {
            UserId = uid,
            PlantId = plant?.Id ?? "",
            PlantName = plant?.CustomName ?? "Cây chưa lưu trong vườn",
            Question = vm.Question,
            UploadedImageUrl = imageUrl ?? "",
            AiModel = _expertAi.Model,
            Result = result,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };

        string diagnosisId;
        try
        {
            diagnosisId = await _diagnosisService.Add(diagnosis);
        }
        catch
        {
            await _imageStorage.Delete(imageUrl, CancellationToken.None);
            throw;
        }

        if (imageUrl == null)
            TempData["Warning"] = "AI đã phân tích thành công nhưng ảnh không lưu được vào lịch sử.";

        return RedirectToAction(nameof(Details), new { id = diagnosisId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        var diagnosis = await _diagnosisService.GetByIdForUser(id, uid);
        return diagnosis == null ? NotFound() : View(diagnosis);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        var diagnosis = await _diagnosisService.GetByIdForUser(id, uid);
        if (diagnosis == null)
            return NotFound();

        await _diagnosisService.Delete(id);
        await _imageStorage.Delete(diagnosis.UploadedImageUrl, CancellationToken.None);
        TempData["Success"] = "Đã xóa phiên tư vấn.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ExpertIndexVM> BuildIndexViewModel(string uid, ExpertIndexVM vm)
    {
        var plants = await _userPlantService.GetAll(uid);
        vm.IsAiConfigured = _expertAi.IsConfigured;
        vm.History = await _diagnosisService.GetByUser(uid);
        vm.PlantOptions = plants
            .OrderBy(x => x.CustomName)
            .Select(x => new SelectListItem(x.CustomName, x.Id, x.Id == vm.PlantId))
            .ToList();
        return vm;
    }
}
