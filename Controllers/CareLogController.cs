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

        model.CreatedAt =
            Timestamp.GetCurrentTimestamp();

        await _service.AddLog(
            plantId,
            model);

        return RedirectToAction(
            nameof(Index),
            new { plantId });
    }

    public async Task<IActionResult> Delete(
        string plantId,
        string id)
    {
        if (await RequireOwnedPlant(plantId) is IActionResult redirect)
            return redirect;

        await _service.DeleteLog(
            plantId,
            id);

        return RedirectToAction(
            nameof(Index),
            new { plantId });
    }
}