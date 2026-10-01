using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HomePlant.Controllers;

public class HomeController : Controller
{
    private readonly PlantSampleService _plantSampleService;
    private readonly ArticleService _articleService;
    private readonly IConfiguration _configuration;
public HomeController(
    PlantSampleService plantSampleService,
    ArticleService articleService,
    IConfiguration configuration)
    {
        _plantSampleService = plantSampleService;
        _articleService = articleService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var plants =
            await _plantSampleService.GetTopPlants();

        ViewBag.FeaturedPlants = plants;

        ViewBag.Articles =
            await _articleService.GetAll();

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms() => View();

    public IActionResult PaymentPolicy() => View();

    public IActionResult Support() => View();

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600)]
    public ContentResult Robots()
    {
        var sitemap = $"{PublicOrigin()}/sitemap.xml";
        return Content($"User-agent: *\nAllow: /\nDisallow: /Admin\nDisallow: /Revenue\nSitemap: {sitemap}\n", "text/plain");
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public ContentResult Sitemap()
    {
        var paths = new[] { "/", "/Home/Index", "/Library", "/Article", "/Community", "/Plans", "/Home/Terms", "/Home/Privacy", "/Home/PaymentPolicy", "/Home/Support" };
        var origin = PublicOrigin();
        var urls = string.Join("", paths.Select(path => $"<url><loc>{System.Security.SecurityElement.Escape(origin + path)}</loc></url>"));
        return Content($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">{urls}</urlset>", "application/xml");
    }

    private string PublicOrigin()
    {
        var configured = _configuration["App:PublicBaseUrl"] ?? _configuration["RENDER_EXTERNAL_URL"];
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return $"{Request.Scheme}://{Request.Host}";
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
