using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomePlant.Controllers;

public class PlantController : Controller
{
    private readonly UserPlantService _userPlantService;
    private readonly PlantTemplateService _templateService;
    private readonly PlantSampleService _samplePlantService;
    private readonly ImageStorageService _imageStorage;
    private readonly CareLogService _careLogService;
    private readonly CareReminderService _careReminderService;
    private readonly AiDiagnosisService _diagnosisService;

    public PlantController(
        UserPlantService userPlantService,
        PlantTemplateService templateService,
        PlantSampleService samplePlantService,
        ImageStorageService imageStorage,
        CareLogService careLogService,
        CareReminderService careReminderService,
        AiDiagnosisService diagnosisService)
    {
        _userPlantService = userPlantService;
        _templateService = templateService;
        _samplePlantService = samplePlantService;
        _imageStorage = imageStorage;
        _careLogService = careLogService;
        _careReminderService = careReminderService;
        _diagnosisService = diagnosisService;
    }

    public async Task<IActionResult> Index()
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plants = await _userPlantService.GetAll(uid);
        var samples = await _samplePlantService.GetAll();
        var speciesMap = samples
            .Select(PlantSpeciesVM.FromSample)
            .ToDictionary(species => species.Id, species => species);
        foreach (var template in await _templateService.GetAll())
            speciesMap.TryAdd(template.Id, PlantSpeciesVM.FromTemplate(template));

        var vm = plants
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new GardenItemVM
            {
                Plant = p,
                Species = speciesMap.GetValueOrDefault(p.TemplateId)
            })
            .ToList();

        return View(vm);
    }

    public async Task<IActionResult> Create(string? speciesId = null, string? diagnosisId = null)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        await PopulateSpecies(speciesId);

        var vm = new PlantCreateVM { PlantSampleId = speciesId ?? "" };
        if (!string.IsNullOrWhiteSpace(diagnosisId) &&
            !await LoadAiScheduleDefaults(uid, diagnosisId, vm))
        {
            TempData["Warning"] = "Không thể nạp lịch AI từ phiên phân tích này.";
        }

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        PlantCreateVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        AiDiagnosisModel? sourceDiagnosis = null;
        if (!string.IsNullOrWhiteSpace(vm.SourceDiagnosisId))
        {
            sourceDiagnosis = await _diagnosisService.GetByIdForUser(vm.SourceDiagnosisId, uid);
            if (sourceDiagnosis == null)
                ModelState.AddModelError(nameof(vm.SourceDiagnosisId), "Phiên phân tích AI không tồn tại hoặc không thuộc tài khoản của bạn.");
        }

        if (!string.IsNullOrWhiteSpace(vm.PlantSampleId) &&
            (vm.PlantSampleId.Contains('/') || await _samplePlantService.GetById(vm.PlantSampleId) == null))
            ModelState.AddModelError(nameof(vm.PlantSampleId), "Loại cây không tồn tại. Vui lòng chọn lại.");

        ValidateUnit(vm.WateringFrequency, vm.WateringFrequencyUnit, nameof(vm.WateringFrequencyUnit));
        ValidateUnit(vm.FertilizingFrequency, vm.FertilizingFrequencyUnit, nameof(vm.FertilizingFrequencyUnit));
        ValidateUnit(vm.RepottingFrequency, vm.RepottingFrequencyUnit, nameof(vm.RepottingFrequencyUnit));

        if (!ModelState.IsValid)
        {
            await PopulateSpecies(vm.PlantSampleId);
            await PopulateAiScheduleContext(uid, vm.SourceDiagnosisId);
            return View(vm);
        }

        try
        {
            await _userPlantService.EnsureCanAdd(uid);
        }
        catch (PlantLimitException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await PopulateSpecies(vm.PlantSampleId);
            await PopulateAiScheduleContext(uid, vm.SourceDiagnosisId);
            return View(vm);
        }

        var now = Timestamp.GetCurrentTimestamp();

        var imageUrl = await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted);

        var plant = new UserPlantModel
        {
            PlantId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            TemplateId = vm.PlantSampleId,
            CustomName = vm.Nickname,
            Status = ToStoredStatus(vm.CurrentStatus),
            ImageUrl = imageUrl ?? "",
            CreatedAt = now,
            PlantedAt = now,
            WateringFrequency = vm.WateringFrequency,
            WateringFrequencyUnit = CareScheduleCalculator.NormalizeUnit(vm.WateringFrequencyUnit),
            FertilizingFrequency = vm.FertilizingFrequency,
            FertilizingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(vm.FertilizingFrequencyUnit),
            RepottingFrequency = vm.RepottingFrequency,
            RepottingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(vm.RepottingFrequencyUnit),
            NextWateringAt = CareScheduleCalculator.NextFrom(now, vm.WateringFrequency, vm.WateringFrequencyUnit),
            NextFertilizingAt = CareScheduleCalculator.NextFrom(now, vm.FertilizingFrequency, vm.FertilizingFrequencyUnit),
            NextRepottingAt = CareScheduleCalculator.NextFrom(now, vm.RepottingFrequency, vm.RepottingFrequencyUnit)
        };

        try
        {
            await _userPlantService.Add(uid, plant);
        }
        catch (PlantLimitException ex)
        {
            await _imageStorage.Delete(imageUrl, CancellationToken.None);
            TempData["Warning"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await _imageStorage.Delete(imageUrl, CancellationToken.None);
            throw;
        }

        TempData["Success"] = "Đã thêm cây vào vườn.";
        if (vm.Photo != null && imageUrl == null)
            TempData["Warning"] = "Cây đã được thêm, nhưng dịch vụ ảnh chưa nhận được file. Bạn có thể chọn lại ảnh trong mục Sửa cây.";

        if (sourceDiagnosis != null)
        {
            if (await _diagnosisService.LinkPlant(sourceDiagnosis.Id, uid, plant.Id, plant.CustomName))
            {
                await _diagnosisService.MarkCareRecommendationsApplied(sourceDiagnosis.Id, now);
                TempData["Success"] = "Đã thêm cây và tạo lịch nhắc theo khuyến nghị AI.";
                return RedirectToAction(nameof(Details), new { id = plant.Id });
            }
            TempData["Warning"] = "Cây đã được thêm nhưng chưa thể liên kết với phiên tư vấn AI.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(
        string id)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plant = await _userPlantService.GetById(uid, id);

        if (plant == null)
            return NotFound();

        return View(await BuildDetailsVM(plant));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plant = await _userPlantService.GetById(uid, id);

        if (plant == null)
            return NotFound();

        await PopulateSpecies(plant.TemplateId, includeLegacySelected: true);

        return View(new PlantEditVM
        {
            Id = plant.Id,
            Nickname = plant.CustomName,
            PlantSampleId = plant.TemplateId,
            CurrentStatus = plant.DisplayStatus,
            ExistingImageUrl = plant.ImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, PlantEditVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var existing = await _userPlantService.GetById(uid, id);

        if (existing == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(vm.PlantSampleId) &&
            (vm.PlantSampleId.Contains('/') || await GetSpecies(vm.PlantSampleId) == null))
            ModelState.AddModelError(nameof(vm.PlantSampleId), "Loài cây không tồn tại. Vui lòng chọn lại.");

        if (!ModelState.IsValid)
        {
            vm.Id = id;
            vm.ExistingImageUrl = existing.ImageUrl;
            await PopulateSpecies(vm.PlantSampleId, includeLegacySelected: true);
            return View(vm);
        }

        var uploadedImage = vm.Photo != null
            ? await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted)
            : null;
        var previousImage = existing.ImageUrl;

        existing.TemplateId = vm.PlantSampleId;
        existing.CustomName = vm.Nickname;
        existing.Status = ToStoredStatus(vm.CurrentStatus);
        existing.ImageUrl = uploadedImage ?? existing.ImageUrl ?? "";

        try
        {
            await _userPlantService.Update(uid, id, existing);
        }
        catch
        {
            await _imageStorage.Delete(uploadedImage, CancellationToken.None);
            throw;
        }

        if (uploadedImage != null)
            await _imageStorage.Delete(previousImage, CancellationToken.None);

        TempData["Success"] = "Đã cập nhật cây.";
        if (vm.Photo != null && uploadedImage == null)
            TempData["Warning"] = "Thông tin đã được lưu, nhưng ảnh mới không tải lên được. Ảnh cũ vẫn được giữ.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSchedule(
        string id,
        [Bind(Prefix = "Schedule")] CareScheduleVM schedule)
    {
        var uid = HttpContext.Session.GetString("Uid");
        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plant = await _userPlantService.GetById(uid, id);
        if (plant == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            var invalidViewModel = await BuildDetailsVM(plant);
            invalidViewModel.Schedule = schedule;
            return View("Details", invalidViewModel);
        }

        var now = Timestamp.GetCurrentTimestamp();
        plant.NextWateringAt = CareScheduleCalculator.Recalculate(
            plant.WateringFrequency, plant.WateringFrequencyUnit,
            schedule.WateringFrequency, schedule.WateringFrequencyUnit,
            plant.NextWateringAt, null, now);
        plant.NextFertilizingAt = CareScheduleCalculator.Recalculate(
            plant.FertilizingFrequency, plant.FertilizingFrequencyUnit,
            schedule.FertilizingFrequency, schedule.FertilizingFrequencyUnit,
            plant.NextFertilizingAt, null, now);
        plant.NextRepottingAt = CareScheduleCalculator.Recalculate(
            plant.RepottingFrequency, plant.RepottingFrequencyUnit,
            schedule.RepottingFrequency, schedule.RepottingFrequencyUnit,
            plant.NextRepottingAt, null, now);
        plant.WateringFrequency = schedule.WateringFrequency;
        plant.WateringFrequencyUnit = CareScheduleCalculator.NormalizeUnit(schedule.WateringFrequencyUnit);
        plant.FertilizingFrequency = schedule.FertilizingFrequency;
        plant.FertilizingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(schedule.FertilizingFrequencyUnit);
        plant.RepottingFrequency = schedule.RepottingFrequency;
        plant.RepottingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(schedule.RepottingFrequencyUnit);

        await _userPlantService.Update(uid, id, plant);
        await _careReminderService.ClearForPlant(uid, id, HttpContext.RequestAborted);
        TempData["Success"] = "Đã cập nhật lịch nhắc chăm sóc.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(
        string id)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plant = await _userPlantService.GetById(uid, id);
        if (plant == null)
            return NotFound();

        await _userPlantService.Delete(uid, id);
        await _imageStorage.Delete(plant.ImageUrl, CancellationToken.None);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSpecies(
        string? selectedId = null,
        bool includeLegacySelected = false)
    {
        var options = (await _samplePlantService.GetAll())
            .OrderBy(plant => plant.Name)
            .Select(plant => new SelectListItem(plant.Name, plant.Id, plant.Id == selectedId))
            .ToList();

        if (includeLegacySelected &&
            !string.IsNullOrWhiteSpace(selectedId) &&
            options.All(option => option.Value != selectedId))
        {
            var legacy = await _templateService.GetById(selectedId);
            if (legacy != null)
                options.Insert(0, new SelectListItem($"{legacy.Name} (dữ liệu cũ)", legacy.Id, true));
        }

        ViewBag.PlantSamples = options;
    }

    private async Task<bool> LoadAiScheduleDefaults(
        string uid,
        string diagnosisId,
        PlantCreateVM vm)
    {
        var diagnosis = await _diagnosisService.GetByIdForUser(diagnosisId, uid);
        if (diagnosis == null ||
            !CareScheduleCalculator.TryCreateSchedule(
                diagnosis.Result?.CareRecommendations,
                out var schedule))
        {
            return false;
        }

        vm.SourceDiagnosisId = diagnosis.Id;
        vm.WateringFrequency = schedule.WateringFrequency;
        vm.WateringFrequencyUnit = schedule.WateringFrequencyUnit;
        vm.FertilizingFrequency = schedule.FertilizingFrequency;
        vm.FertilizingFrequencyUnit = schedule.FertilizingFrequencyUnit;
        vm.RepottingFrequency = schedule.RepottingFrequency;
        vm.RepottingFrequencyUnit = schedule.RepottingFrequencyUnit;
        SetAiScheduleContext(diagnosis.Result?.IdentifiedPlant);
        return true;
    }

    private async Task PopulateAiScheduleContext(string uid, string? diagnosisId)
    {
        if (string.IsNullOrWhiteSpace(diagnosisId))
            return;

        var diagnosis = await _diagnosisService.GetByIdForUser(diagnosisId, uid);
        if (diagnosis != null)
            SetAiScheduleContext(diagnosis.Result?.IdentifiedPlant);
    }

    private void SetAiScheduleContext(string? identifiedPlant)
    {
        ViewBag.AiScheduleLoaded = true;
        ViewBag.AiIdentifiedPlant = identifiedPlant ?? "";
    }

    private async Task<PlantSpeciesVM?> GetSpecies(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var sample = await _samplePlantService.GetById(id);
        if (sample != null)
            return PlantSpeciesVM.FromSample(sample);

        var legacy = await _templateService.GetById(id);
        return legacy == null ? null : PlantSpeciesVM.FromTemplate(legacy);
    }

    private async Task<PlantDetailsVM> BuildDetailsVM(UserPlantModel plant)
    {
        var speciesTask = GetSpecies(plant.TemplateId);
        var logsTask = _careLogService.GetLogs(plant.Id);
        await Task.WhenAll(speciesTask, logsTask);

        return new PlantDetailsVM
        {
            Plant = plant,
            Species = speciesTask.Result,
            CareLogs = logsTask.Result,
            Schedule = new CareScheduleVM
            {
                WateringFrequency = plant.WateringFrequency,
                WateringFrequencyUnit = CareScheduleCalculator.NormalizeUnit(plant.WateringFrequencyUnit),
                FertilizingFrequency = plant.FertilizingFrequency,
                FertilizingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(plant.FertilizingFrequencyUnit),
                RepottingFrequency = plant.RepottingFrequency,
                RepottingFrequencyUnit = CareScheduleCalculator.NormalizeUnit(plant.RepottingFrequencyUnit)
            }
        };
    }

    private void ValidateUnit(int? frequency, string? unit, string field)
    {
        if (frequency.HasValue && unit is not ("Seconds" or "Minutes" or "Hours" or "Days"))
            ModelState.AddModelError(field, "Đơn vị thời gian không hợp lệ.");
    }

    private static string ToStoredStatus(string vietnameseStatus) => vietnameseStatus switch
    {
        "Khỏe mạnh" => "healthy",
        "Cần chú ý" => "warning",
        "Bị bệnh" => "sick",
        _ => "healthy"
    };
}
