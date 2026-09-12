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

    public PlantController(
        UserPlantService userPlantService,
        PlantTemplateService templateService,
        PlantSampleService samplePlantService,
        ImageStorageService imageStorage)
    {
        _userPlantService = userPlantService;
        _templateService = templateService;
        _samplePlantService = samplePlantService;
        _imageStorage = imageStorage;
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

    public async Task<IActionResult> Create(string? speciesId = null)
    {
        if (HttpContext.Session.GetString("Uid") == null)
            return RedirectToAction("Login", "Account");

        await PopulateSpecies(speciesId);

        return View(new PlantCreateVM { PlantSampleId = speciesId ?? "" });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        PlantCreateVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        if (!string.IsNullOrWhiteSpace(vm.PlantSampleId) &&
            (vm.PlantSampleId.Contains('/') || await _samplePlantService.GetById(vm.PlantSampleId) == null))
            ModelState.AddModelError(nameof(vm.PlantSampleId), "Loại cây không tồn tại. Vui lòng chọn lại.");

        if (!ModelState.IsValid)
        {
            await PopulateSpecies(vm.PlantSampleId);
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
            FertilizingFrequency = vm.FertilizingFrequency,
            RepottingFrequency = vm.RepottingFrequency,
            NextWateringAt = CareScheduleCalculator.NextFrom(now, vm.WateringFrequency),
            NextFertilizingAt = CareScheduleCalculator.NextFrom(now, vm.FertilizingFrequency),
            NextRepottingAt = CareScheduleCalculator.NextFrom(now, vm.RepottingFrequency)
        };

        try
        {
            await _userPlantService.Add(uid, plant);
        }
        catch
        {
            await _imageStorage.Delete(imageUrl, CancellationToken.None);
            throw;
        }

        TempData["Success"] = "Đã thêm cây vào vườn.";
        if (vm.Photo != null && imageUrl == null)
            TempData["Warning"] = "Cây đã được thêm, nhưng dịch vụ ảnh chưa nhận được file. Bạn có thể chọn lại ảnh trong mục Sửa cây.";

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

        ViewBag.Species = await GetSpecies(plant.TemplateId);

        return View(plant);
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
            ExistingImageUrl = plant.ImageUrl,
            WateringFrequency = plant.WateringFrequency,
            FertilizingFrequency = plant.FertilizingFrequency,
            RepottingFrequency = plant.RepottingFrequency
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

        var now = Timestamp.GetCurrentTimestamp();
        existing.TemplateId = vm.PlantSampleId;
        existing.CustomName = vm.Nickname;
        existing.Status = ToStoredStatus(vm.CurrentStatus);
        existing.ImageUrl = uploadedImage ?? existing.ImageUrl ?? "";
        existing.NextWateringAt = CareScheduleCalculator.Recalculate(
            existing.WateringFrequency, vm.WateringFrequency, existing.NextWateringAt, existing.LastWatered, now);
        existing.NextFertilizingAt = CareScheduleCalculator.Recalculate(
            existing.FertilizingFrequency, vm.FertilizingFrequency, existing.NextFertilizingAt, existing.LastFertilized, now);
        existing.NextRepottingAt = CareScheduleCalculator.Recalculate(
            existing.RepottingFrequency, vm.RepottingFrequency, existing.NextRepottingAt, existing.LastRepotted, now);
        existing.WateringFrequency = vm.WateringFrequency;
        existing.FertilizingFrequency = vm.FertilizingFrequency;
        existing.RepottingFrequency = vm.RepottingFrequency;

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

    private static string ToStoredStatus(string vietnameseStatus) => vietnameseStatus switch
    {
        "Khỏe mạnh" => "healthy",
        "Cần chú ý" => "warning",
        "Bị bệnh" => "sick",
        _ => "healthy"
    };
}
