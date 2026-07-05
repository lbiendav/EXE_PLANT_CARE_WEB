using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class AdminController : Controller
{
    private readonly UserService _userService;
    private readonly ArticleService _articleService;
    private readonly PlantTemplateService _templateService;
    private readonly PlantSampleService _samplePlantService;
    private readonly UserPlantService _userPlantService;
    private readonly CommunityPostService _communityPostService;
    private readonly QaThreadService _qaThreadService;
    private readonly AiDiagnosisService _aiDiagnosisService;
    private readonly ImgBbService _imgBbService;

    public AdminController(
        UserService userService,
        ArticleService articleService,
        PlantTemplateService templateService,
        PlantSampleService samplePlantService,
        UserPlantService userPlantService,
        CommunityPostService communityPostService,
        QaThreadService qaThreadService,
        AiDiagnosisService aiDiagnosisService,
        ImgBbService imgBbService)
    {
        _userService = userService;
        _articleService = articleService;
        _templateService = templateService;
        _samplePlantService = samplePlantService;
        _userPlantService = userPlantService;
        _communityPostService = communityPostService;
        _qaThreadService = qaThreadService;
        _aiDiagnosisService = aiDiagnosisService;
        _imgBbService = imgBbService;
    }

    private IActionResult? RequireAdmin()
    {
        var role = HttpContext.Session.GetString("Role");

        if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Login", "Account");

        return null;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var users = await _userService.GetAll();
        var articles = await _articleService.GetAll();
        var templates = await _templateService.GetAll();
        var samplePlants = await _samplePlantService.GetAll();
        var communityPosts = await _communityPostService.GetAll();
        var qaThreads = await _qaThreadService.GetAll();
        var aiDiagnoses = await _aiDiagnosisService.GetAll();
        var gardenPlantCount = await _userPlantService.CountAll();

        var vm = new AdminDashboardVM
        {
            UserCount = users.Count,
            ArticleCount = articles.Count,
            PlantTemplateCount = templates.Count,
            SamplePlantCount = samplePlants.Count,
            GardenPlantCount = gardenPlantCount,
            CommunityPostCount = communityPosts.Count,
            QaThreadCount = qaThreads.Count,
            AiDiagnosisCount = aiDiagnoses.Count
        };

        return View(vm);
    }

    public async Task<IActionResult> Users()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var users = await _userService.GetAll();

        return View(users);
    }

    public async Task<IActionResult> Ban(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _userService.BanUser(id);

        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> UnBan(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _userService.UnBanUser(id);

        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> PlantTemplates()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var templates = await _templateService.GetAll();

        return View(templates);
    }

    public async Task<IActionResult> DeletePlantTemplate(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _templateService.Delete(id);

        return RedirectToAction(nameof(PlantTemplates));
    }

    public async Task<IActionResult> SamplePlants()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var samples = await _samplePlantService.GetAll();

        return View(samples);
    }

    public async Task<IActionResult> DeleteSamplePlant(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _samplePlantService.Delete(id);

        return RedirectToAction(nameof(SamplePlants));
    }

    public IActionResult CreateSamplePlant()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        return View(new PlantSampleFormVM());
    }

    [HttpPost]
    public async Task<IActionResult> CreateSamplePlant(
        PlantSampleFormVM vm)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(vm.Name), "Vui lòng nhập tên cây");
            return View(vm);
        }

        var plant = new PlantSampleModel
        {
            Name = vm.Name,
            ScientificName = vm.ScientificName,
            Description = vm.Description,
            Image = vm.Photo != null ? await _imgBbService.Upload(vm.Photo) : vm.ExistingImageUrl,
            CreatedAt = Timestamp.GetCurrentTimestamp(),
            Care = new CareModel
            {
                Light = vm.Light,
                Water = vm.Water,
                Soil = vm.Soil,
                Fertilizer = vm.Fertilizer
            },
            Diseases = CleanDiseases(vm.Diseases)
        };

        await _samplePlantService.Add(plant);

        return RedirectToAction(nameof(SamplePlants));
    }

    public async Task<IActionResult> EditSamplePlant(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var plant = await _samplePlantService.GetById(id);

        if (plant == null)
            return NotFound();

        var vm = new PlantSampleFormVM
        {
            Id = plant.Id,
            Name = plant.Name,
            ScientificName = plant.ScientificName,
            Description = plant.Description,
            ExistingImageUrl = plant.Image,
            Light = plant.Care?.Light,
            Water = plant.Care?.Water,
            Soil = plant.Care?.Soil,
            Fertilizer = plant.Care?.Fertilizer,
            Diseases = plant.Diseases ?? new List<DiseaseModel>()
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> EditSamplePlant(
        string id,
        PlantSampleFormVM vm)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(vm.Name), "Vui lòng nhập tên cây");
            return View(vm);
        }

        var existing = await _samplePlantService.GetById(id);

        if (existing == null)
            return NotFound();

        var plant = new PlantSampleModel
        {
            Id = id,
            Name = vm.Name,
            ScientificName = vm.ScientificName,
            Description = vm.Description,
            Image = vm.Photo != null ? await _imgBbService.Upload(vm.Photo) : vm.ExistingImageUrl,
            CreatedAt = existing.CreatedAt,
            Care = new CareModel
            {
                Light = vm.Light,
                Water = vm.Water,
                Soil = vm.Soil,
                Fertilizer = vm.Fertilizer
            },
            Diseases = CleanDiseases(vm.Diseases)
        };

        await _samplePlantService.Update(id, plant);

        return RedirectToAction(nameof(SamplePlants));
    }

    private static List<DiseaseModel> CleanDiseases(List<DiseaseModel> diseases)
    {
        return diseases
            .Where(d => !string.IsNullOrWhiteSpace(d.Issue))
            .ToList();
    }

    public async Task<IActionResult> CommunityPosts()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var posts = await _communityPostService.GetAll();

        return View(posts);
    }

    public async Task<IActionResult> DeleteCommunityPost(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _communityPostService.Delete(id);

        return RedirectToAction(nameof(CommunityPosts));
    }

    public async Task<IActionResult> QaThreads()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var threads = await _qaThreadService.GetAll();

        return View(threads);
    }

    public async Task<IActionResult> DeleteQaThread(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _qaThreadService.Delete(id);

        return RedirectToAction(nameof(QaThreads));
    }

    public async Task<IActionResult> AiDiagnoses()
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        var diagnoses = await _aiDiagnosisService.GetAll();

        return View(diagnoses);
    }

    public async Task<IActionResult> DeleteAiDiagnosis(string id)
    {
        if (RequireAdmin() is IActionResult redirect)
            return redirect;

        await _aiDiagnosisService.Delete(id);

        return RedirectToAction(nameof(AiDiagnoses));
    }
}
