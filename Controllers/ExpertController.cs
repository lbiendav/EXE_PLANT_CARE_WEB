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
    private readonly CareReminderService _careReminderService;
    private readonly AiQuotaService _quotaService;

    public ExpertController(
        UserPlantService userPlantService,
        AiDiagnosisService diagnosisService,
        PlantExpertAiService expertAi,
        ImageStorageService imageStorage,
        CareReminderService careReminderService,
        AiQuotaService quotaService)
    {
        _userPlantService = userPlantService;
        _diagnosisService = diagnosisService;
        _expertAi = expertAi;
        _imageStorage = imageStorage;
        _careReminderService = careReminderService;
        _quotaService = quotaService;
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

        try
        {
            await _quotaService.Reserve(uid, vm.RequestId);
        }
        catch (AiQuotaException exception)
        {
            ModelState.AddModelError("", exception.Message);
            return View("Index", await BuildIndexViewModel(uid, vm));
        }

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
            await _quotaService.Release(uid, vm.RequestId);
            ModelState.AddModelError("", exception.Message);
            return View("Index", await BuildIndexViewModel(uid, vm));
        }
        catch
        {
            await _quotaService.Release(uid, vm.RequestId);
            throw;
        }

        string? imageUrl;
        try
        {
            imageUrl = await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted);
        }
        catch
        {
            await _quotaService.Release(uid, vm.RequestId);
            throw;
        }
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
            diagnosisId = await _quotaService.Complete(uid, vm.RequestId, diagnosis);
        }
        catch
        {
            await _quotaService.Release(uid, vm.RequestId);
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
        if (diagnosis == null)
            return NotFound();

        ViewBag.LinkedPlantExists = !string.IsNullOrWhiteSpace(diagnosis.PlantId) &&
            await _userPlantService.GetById(uid, diagnosis.PlantId) != null;
        return View(diagnosis);
    }

    [HttpPost]
    public async Task<IActionResult> ApplyCareRecommendations(string id)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        var diagnosis = await _diagnosisService.GetByIdForUser(id, uid);
        if (diagnosis == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(diagnosis.PlantId))
        {
            TempData["Warning"] = "Phiên tư vấn này chưa liên kết với cây trong vườn nên chưa thể tạo lịch nhắc.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var plant = await _userPlantService.GetById(uid, diagnosis.PlantId);
        if (plant == null)
        {
            TempData["Warning"] = "Không tìm thấy cây đã liên kết với phiên tư vấn này.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!CareScheduleCalculator.TryCreateSchedule(
                diagnosis.Result?.CareRecommendations,
                out var schedule))
        {
            TempData["Warning"] = "Khuyến nghị này chưa đủ tin cậy để áp dụng tự động.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = Timestamp.GetCurrentTimestamp();
        plant.NextWateringAt = CareScheduleCalculator.Recalculate(
            plant.WateringFrequency, plant.WateringFrequencyUnit,
            schedule.WateringFrequency, schedule.WateringFrequencyUnit,
            plant.NextWateringAt, plant.LastWatered, now);
        plant.NextFertilizingAt = CareScheduleCalculator.Recalculate(
            plant.FertilizingFrequency, plant.FertilizingFrequencyUnit,
            schedule.FertilizingFrequency, schedule.FertilizingFrequencyUnit,
            plant.NextFertilizingAt, plant.LastFertilized, now);
        plant.NextRepottingAt = CareScheduleCalculator.Recalculate(
            plant.RepottingFrequency, plant.RepottingFrequencyUnit,
            schedule.RepottingFrequency, schedule.RepottingFrequencyUnit,
            plant.NextRepottingAt, plant.LastRepotted, now);
        plant.WateringFrequency = schedule.WateringFrequency;
        plant.WateringFrequencyUnit = schedule.WateringFrequencyUnit;
        plant.FertilizingFrequency = schedule.FertilizingFrequency;
        plant.FertilizingFrequencyUnit = schedule.FertilizingFrequencyUnit;
        plant.RepottingFrequency = schedule.RepottingFrequency;
        plant.RepottingFrequencyUnit = schedule.RepottingFrequencyUnit;

        await _userPlantService.Update(uid, plant.Id, plant);
        await _careReminderService.ClearForPlant(uid, plant.Id, HttpContext.RequestAborted);
        await _diagnosisService.MarkCareRecommendationsApplied(id, now);

        TempData["Success"] = $"Đã áp dụng lịch chăm sóc AI cho {plant.CustomName}.";
        return RedirectToAction(nameof(Details), new { id });
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
