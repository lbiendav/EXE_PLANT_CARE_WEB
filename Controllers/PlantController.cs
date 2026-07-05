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

        if (!ModelState.IsValid)
        {
            await PopulateTemplates();
            return View(vm);
        }

        var now = Timestamp.GetCurrentTimestamp();

        var plant = new UserPlantModel
        {
            PlantId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            TemplateId = vm.PlantSampleId,
            CustomName = vm.Nickname,
            Status = ToStoredStatus(vm.CurrentStatus),
            ImageUrl = await _imgBbService.Upload(vm.Photo),
            CreatedAt = now,
            PlantedAt = now
        };

        await _userPlantService.Add(uid, plant);

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

    public async Task<IActionResult> Delete(
        string id)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        await _userPlantService.Delete(uid, id);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateTemplates()
    {
        var templates = await _templateService.GetAll();

        ViewBag.PlantSamples = new SelectList(
            templates, "Id", "Name");
    }

    private static string ToStoredStatus(string vietnameseStatus) => vietnameseStatus switch
    {
        "Khỏe mạnh" => "healthy",
        "Cần chú ý" => "warning",
        "Bị bệnh" => "sick",
        _ => "healthy"
    };
}
