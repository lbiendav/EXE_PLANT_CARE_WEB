using Google.Cloud.Firestore;
using HomePlant.Filters;
using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

[AdminOnly]
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
    private readonly ImageStorageService _imageStorage;

    public AdminController(
        UserService userService,
        ArticleService articleService,
        PlantTemplateService templateService,
        PlantSampleService samplePlantService,
        UserPlantService userPlantService,
        CommunityPostService communityPostService,
        QaThreadService qaThreadService,
        AiDiagnosisService aiDiagnosisService,
        ImageStorageService imageStorage)
    {
        _userService = userService;
        _articleService = articleService;
        _templateService = templateService;
        _samplePlantService = samplePlantService;
        _userPlantService = userPlantService;
        _communityPostService = communityPostService;
        _qaThreadService = qaThreadService;
        _aiDiagnosisService = aiDiagnosisService;
        _imageStorage = imageStorage;
    }

    public async Task<IActionResult> Dashboard()
    {
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
        var users = await _userService.GetAll();

        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> Ban(string id)
    {
        if (id == HttpContext.Session.GetString("Uid"))
        {
            TempData["Warning"] = "Bạn không thể tự khóa tài khoản quản trị đang sử dụng.";
            return RedirectToAction(nameof(Users));
        }

        await _userService.BanUser(id);

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> UnBan(string id)
    {
        await _userService.UnBanUser(id);

        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> PlantTemplates()
    {
        var templates = await _templateService.GetAll();

        return View(templates);
    }

    [HttpPost]
    public async Task<IActionResult> DeletePlantTemplate(string id)
    {
        await _templateService.Delete(id);

        return RedirectToAction(nameof(PlantTemplates));
    }

    public async Task<IActionResult> SamplePlants()
    {
        var samples = await _samplePlantService.GetAll();

        return View(samples);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSamplePlant(string id)
    {
        var plant = await _samplePlantService.GetById(id);
        if (plant == null)
            return NotFound();

        await _samplePlantService.Delete(id);
        await _imageStorage.Delete(plant.Image, CancellationToken.None);

        return RedirectToAction(nameof(SamplePlants));
    }

    public IActionResult CreateSamplePlant()
    {
        return View(new PlantSampleFormVM());
    }

    [HttpPost]
    public async Task<IActionResult> CreateSamplePlant(
        PlantSampleFormVM vm)
    {
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
            Image = vm.Photo != null
                ? await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted) ?? ""
                : vm.ExistingImageUrl,
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

        try
        {
            await _samplePlantService.Add(plant);
        }
        catch
        {
            await _imageStorage.Delete(plant.Image, CancellationToken.None);
            throw;
        }

        TempData["Success"] = "Đã thêm cây vào thư viện.";
        if (vm.Photo != null && string.IsNullOrEmpty(plant.Image))
            TempData["Warning"] = "Cây đã được thêm, nhưng dịch vụ ảnh chưa nhận được file. Bạn có thể chọn lại ảnh khi sửa cây.";

        return RedirectToAction(nameof(SamplePlants));
    }

    public async Task<IActionResult> EditSamplePlant(string id)
    {
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
        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(vm.Name), "Vui lòng nhập tên cây");
            return View(vm);
        }

        var existing = await _samplePlantService.GetById(id);

        if (existing == null)
            return NotFound();

        var uploadedImage = vm.Photo != null
            ? await _imageStorage.Upload(vm.Photo, HttpContext.RequestAborted)
            : null;

        var plant = new PlantSampleModel
        {
            Id = id,
            Name = vm.Name,
            ScientificName = vm.ScientificName,
            Description = vm.Description,
            Image = uploadedImage ?? existing.Image,
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

        try
        {
            await _samplePlantService.Update(id, plant);
        }
        catch
        {
            await _imageStorage.Delete(uploadedImage, CancellationToken.None);
            throw;
        }

        if (uploadedImage != null)
            await _imageStorage.Delete(existing.Image, CancellationToken.None);

        TempData["Success"] = "Đã cập nhật cây trong thư viện.";
        if (vm.Photo != null && uploadedImage == null)
            TempData["Warning"] = "Thông tin đã được lưu, nhưng ảnh mới không tải lên được. Ảnh cũ vẫn được giữ.";

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
        var posts = await _communityPostService.GetAll();

        return View(posts);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCommunityPost(string id)
    {
        await _communityPostService.Delete(id);

        return RedirectToAction(nameof(CommunityPosts));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCommunityPostStatus(string id, string status)
    {
        if (status is not ("approved" or "hidden" or "pending"))
            return BadRequest();

        await _communityPostService.UpdateStatus(id, status);
        TempData["Success"] = "Đã cập nhật trạng thái bài đăng.";
        return RedirectToAction(nameof(CommunityPosts));
    }

    public async Task<IActionResult> QaThreads()
    {
        var threads = await _qaThreadService.GetAll();

        return View(threads);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQaThread(string id)
    {
        await _qaThreadService.Delete(id);

        return RedirectToAction(nameof(QaThreads));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateQaThreadStatus(string id, string status)
    {
        if (status is not ("pending" or "processing" or "resolved"))
            return BadRequest();

        await _qaThreadService.UpdateStatus(id, status);
        TempData["Success"] = "Đã cập nhật trạng thái câu hỏi.";
        return RedirectToAction(nameof(QaThreads));
    }

    public async Task<IActionResult> AiDiagnoses()
    {
        var usersTask = _userService.GetAll();
        var diagnosesTask = _aiDiagnosisService.GetAll();
        await Task.WhenAll(usersTask, diagnosesTask);

        var usersById = usersTask.Result
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var diagnoses = diagnosesTask.Result;
        var vm = new AdminAiDiagnosesVM
        {
            TotalCount = diagnoses.Count,
            UserGroups = diagnoses
                .GroupBy(diagnosis => diagnosis.UserId ?? "")
                .Select(group => new AdminUserGroupVM<AiDiagnosisModel>
                {
                    UserId = group.Key,
                    User = usersById.GetValueOrDefault(group.Key),
                    Items = group.OrderByDescending(item => item.CreatedAt).ToList()
                })
                .OrderBy(group => group.DisplayName)
                .ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> GardenPlants()
    {
        var usersTask = _userService.GetAll();
        var plantsTask = _userPlantService.GetAllForAdmin();
        var samplesTask = _samplePlantService.GetAll();
        var templatesTask = _templateService.GetAll();
        await Task.WhenAll(usersTask, plantsTask, samplesTask, templatesTask);

        var usersById = usersTask.Result
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var speciesNames = samplesTask.Result
            .GroupBy(plant => plant.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        foreach (var template in templatesTask.Result)
            speciesNames.TryAdd(template.Id, template.Name);

        var plants = plantsTask.Result;
        var vm = new AdminGardenPlantsVM
        {
            TotalCount = plants.Count,
            UserGroups = plants
                .GroupBy(item => item.UserId)
                .Select(group => new AdminUserGroupVM<AdminGardenPlantItemVM>
                {
                    UserId = group.Key,
                    User = usersById.GetValueOrDefault(group.Key),
                    Items = group
                        .Select(item => new AdminGardenPlantItemVM
                        {
                            Plant = item.Plant,
                            SpeciesName = speciesNames.GetValueOrDefault(
                                item.Plant.TemplateId,
                                "Loại cây chưa xác định")
                        })
                        .OrderBy(item => item.Plant.CustomName)
                        .ToList()
                })
                .OrderBy(group => group.DisplayName)
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGardenPlant(string userId, string id)
    {
        var plant = await _userPlantService.GetById(userId, id);
        if (plant == null)
            return NotFound();

        await _userPlantService.Delete(userId, id);
        await _imageStorage.Delete(plant.ImageUrl, CancellationToken.None);
        TempData["Success"] = "Đã xóa cây khỏi vườn của người dùng.";

        return RedirectToAction(nameof(GardenPlants));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAiDiagnosis(string id)
    {
        var diagnosis = await _aiDiagnosisService.GetById(id);
        if (diagnosis == null)
            return NotFound();

        await _aiDiagnosisService.Delete(id);
        await _imageStorage.Delete(diagnosis.UploadedImageUrl, CancellationToken.None);

        return RedirectToAction(nameof(AiDiagnoses));
    }
}
