using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class LibraryController : Controller
{
    private readonly PlantSampleService _service;

    public LibraryController(
        PlantSampleService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var plants =
            await _service.GetAll();

        return View(plants);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var plant =
            await _service.GetById(id);

        if (plant == null)
            return NotFound();

        return View(plant);
    }
}