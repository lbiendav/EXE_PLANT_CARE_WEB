using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HomePlant.Controllers;

public class HomeController : Controller
{
    private readonly PlantSampleService _plantSampleService;
    private readonly ArticleService _articleService;
public HomeController(
    PlantSampleService plantSampleService,
    ArticleService articleService)
    {
        _plantSampleService = plantSampleService;
        _articleService = articleService;
    }

    public async Task<IActionResult> Index()
    {
        var plants =
            await _plantSampleService.GetTopPlants();

        Console.WriteLine(
            $"Plants Count = {plants.Count}");

        foreach (var p in plants)
        {
            Console.WriteLine(
                $"{p.Name} - {p.Image}");
        }

        ViewBag.FeaturedPlants = plants;

        ViewBag.Articles =
            await _articleService.GetAll();

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View(
            new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
    }
}
