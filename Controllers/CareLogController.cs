using Google.Cloud.Firestore;
using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class CareLogController : Controller
{
    private readonly CareLogService _service;

    public CareLogController(
        CareLogService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(
        string plantId)
    {
        ViewBag.PlantId = plantId;

        var logs =
            await _service.GetLogs(plantId);

        return View(logs);
    }

    public IActionResult Create(
        string plantId)
    {
        ViewBag.PlantId = plantId;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string plantId,
        CareLogModel model)
    {
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
        await _service.DeleteLog(
            plantId,
            id);

        return RedirectToAction(
            nameof(Index),
            new { plantId });
    }
}