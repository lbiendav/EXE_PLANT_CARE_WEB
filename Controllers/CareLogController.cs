using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class CareLogController : Controller
{
    private readonly CareLogService _service;
    private readonly UserPlantService _userPlantService;

    public CareLogController(
        CareLogService service,
        UserPlantService userPlantService)
    {
        _service = service;
        _userPlantService = userPlantService;
    }

    private async Task<IActionResult?> RequireOwnedPlant(
        string plantId)
    {
        var uid = HttpContext.Session.GetString("Uid");

        if (uid == null)
            return RedirectToAction("Login", "Account");

        var plant = await _userPlantService.GetById(uid, plantId);

        if (plant == null)
            return NotFound();

        return null;
    }

    public async Task<IActionResult> Index(
        string plantId)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        ViewBag.PlantId = plantId;

        var logs =
            await _service.GetLogs(plantId);

        return View(logs);
    }

    public async Task<IActionResult> Create(
        string plantId)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        ViewBag.PlantId = plantId;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string plantId,
        CareLogModel model)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        // Identity and timestamps belong to the server, never to submitted fields.
        model.UserId = HttpContext.Session.GetString("Uid")!;
        model.Note ??= "";
        model.ImageUrl = "";
        if (model.ActionType is not ("Watering" or "Fertilizing" or "Repotting" or "Observation"))
        {
            ModelState.AddModelError(nameof(model.ActionType), "Vui lòng chọn hoạt động hợp lệ.");
            ViewBag.PlantId = plantId;
            return View(model);
        }

        model.CreatedAt =
            Timestamp.GetCurrentTimestamp();

        await _service.AddLog(
            model.UserId,
            plantId,
            model);

        return RedirectToAction(
            nameof(Index),
            new { plantId });
    }

    [HttpPost]
    public async Task<IActionResult> QuickCreate(string plantId, string actionType)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        if (actionType is not ("Watering" or "Fertilizing" or "Repotting"))
            return BadRequest();

        var uid = HttpContext.Session.GetString("Uid")!;
        await _service.AddLog(uid, plantId, new CareLogModel
        {
            UserId = uid,
            ActionType = actionType,
            Note = "",
            ImageUrl = "",
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });

        TempData["Success"] = actionType switch
        {
            "Watering" => "Đã ghi nhận tưới nước và cập nhật lịch tiếp theo.",
            "Fertilizing" => "Đã ghi nhận bón phân và cập nhật lịch tiếp theo.",
            _ => "Đã ghi nhận thay chậu và cập nhật lịch tiếp theo."
        };
        return RedirectToAction("Details", "Plant", new { id = plantId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(
        string plantId,
        string id,
        string? returnUrl = null)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        await _service.DeleteLog(
            HttpContext.Session.GetString("Uid")!,
            plantId,
            id);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(
            nameof(Index),
            new { plantId });
    }
}
