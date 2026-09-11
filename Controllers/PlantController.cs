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
    private readonly ImgBbService _imgBbService;

    public PlantController(
        UserPlantService userPlantService,
        PlantTemplateService templateService,
        ImgBbService imgBbService)
    {
        _userPlantService = userPlantService;
        _templateService = templateService;
        _imgBbService = imgBbService;
    }

    public async Task<IActionResult> Index()
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plants = await _userPlantService.GetAll(uid);
        var templates = await _templateService.GetAll();
        var templateMap = templates.ToDictionary(t => t.Id, t => t);

        var vm = plants
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new GardenItemVM
            {
                Plant = p,
                Template = templateMap.GetValueOrDefault(p.TemplateId)
            })
            .ToList();

        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        if (HttpContext.Session.GetString("Uid") == null)
            return RedirectToAction("Login", "Account");

        await PopulateTemplates();

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        PlantCreateVM vm)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        if (!string.IsNullOrWhiteSpace(vm.PlantSampleId) &&
            (vm.PlantSampleId.Contains('/') || await _templateService.GetById(vm.PlantSampleId) == null))
            ModelState.AddModelError(nameof(vm.PlantSampleId), "Loại cây không tồn tại. Vui lòng chọn lại.");

        if (!ModelState.IsValid)
        {
            await PopulateTemplates();
            return View(vm);
        }

        var now = Timestamp.GetCurrentTimestamp();

        var imageUrl = await _imgBbService.Upload(vm.Photo, HttpContext.RequestAborted);

        var plant = new UserPlantModel
        {
            PlantId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            TemplateId = vm.PlantSampleId,
            CustomName = vm.Nickname,
            Status = ToStoredStatus(vm.CurrentStatus),
            ImageUrl = imageUrl ?? "",
            CreatedAt = now,
            PlantedAt = now
        };

        await _userPlantService.Add(uid, plant);

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

        if (!string.IsNullOrEmpty(plant.TemplateId))
        {
            ViewBag.Template = await _templateService.GetById(plant.TemplateId);
        }

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

        await PopulateTemplates(plant.TemplateId);

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
            (vm.PlantSampleId.Contains('/') || await _templateService.GetById(vm.PlantSampleId) == null))
            ModelState.AddModelError(nameof(vm.PlantSampleId), "Loại cây không tồn tại. Vui lòng chọn lại.");

        if (!ModelState.IsValid)
        {
            vm.Id = id;
            vm.ExistingImageUrl = existing.ImageUrl;
            await PopulateTemplates(vm.PlantSampleId);
            return View(vm);
        }

        var uploadedImage = vm.Photo != null
            ? await _imgBbService.Upload(vm.Photo, HttpContext.RequestAborted)
            : null;

        existing.TemplateId = vm.PlantSampleId;
        existing.CustomName = vm.Nickname;
        existing.Status = ToStoredStatus(vm.CurrentStatus);
        existing.ImageUrl = uploadedImage ?? existing.ImageUrl ?? "";

        await _userPlantService.Update(uid, id, existing);

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

        await _userPlantService.Delete(uid, id);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateTemplates(string? selectedId = null)
    {
        var templates = await _templateService.GetAll();

        ViewBag.PlantSamples = new SelectList(
            templates, "Id", "Name", selectedId);
    }

    private static string ToStoredStatus(string vietnameseStatus) => vietnameseStatus switch
    {
        "Khỏe mạnh" => "healthy",
        "Cần chú ý" => "warning",
        "Bị bệnh" => "sick",
        _ => "healthy"
    };
}
