using HomePlant.Models;
using HomePlant.Services;
using HomePlant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HomePlant.Controllers;

public class HomeController : Controller
{
    private readonly PlantSampleService _plantSampleService;
    private readonly ArticleService _articleService;
    private readonly FeedbackService _feedbackService;
    private readonly IConfiguration _configuration;

    public HomeController(
        PlantSampleService plantSampleService,
        ArticleService articleService,
        FeedbackService feedbackService,
        IConfiguration configuration)
    {
        _plantSampleService = plantSampleService;
        _articleService = articleService;
        _feedbackService = feedbackService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            ViewBag.Articles = await _articleService.GetAll();
        }
        catch
        {
            ViewBag.Articles = new List<ArticleModel>();
        }

        try
        {
            ViewBag.Feedbacks = await _feedbackService.GetFeaturedFeedbacksAsync();
        }
        catch
        {
            ViewBag.Feedbacks = FeedbackService.GetDefaultFeedbacks();
        }

        return View();
    }

    [HttpGet("/Feedback")]
    public IActionResult Feedback()
    {
        return Redirect("/#feedback-section");
    }

    [HttpPost("/api/feedback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackSubmitViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { success = false, message = string.Join("; ", errors) });
        }

        var tagType = model.Tag switch
        {
            var t when t.Contains("CỨU CÂY", StringComparison.OrdinalIgnoreCase) => "success",
            var t when t.Contains("GOLD", StringComparison.OrdinalIgnoreCase) || t.Contains("CHUYÊN GIA", StringComparison.OrdinalIgnoreCase) => "gold",
            var t when t.Contains("NGƯỜI MỚI", StringComparison.OrdinalIgnoreCase) => "info",
            _ => "copper"
        };

        var tagIcon = model.Tag switch
        {
            var t when t.Contains("CỨU CÂY", StringComparison.OrdinalIgnoreCase) => "🌿",
            var t when t.Contains("GOLD", StringComparison.OrdinalIgnoreCase) || t.Contains("CHUYÊN GIA", StringComparison.OrdinalIgnoreCase) => "✨",
            var t when t.Contains("NGƯỜI MỚI", StringComparison.OrdinalIgnoreCase) => "🌱",
            _ => "🪴"
        };

        var subtitle = string.IsNullOrWhiteSpace(model.UserSubtitle)
            ? $"{DateTime.UtcNow:dd/MM/yyyy} · Thành viên HomePlant"
            : model.UserSubtitle.Trim();

        var feedback = new FeedbackModel
        {
            UserName = model.UserName.Trim(),
            UserSubtitle = subtitle,
            Tag = model.Tag.Trim().ToUpperInvariant(),
            TagIcon = tagIcon,
            TagType = tagType,
            Rating = Math.Clamp(model.Rating, 1, 5),
            Title = model.Title.Trim(),
            Content = model.Content.Trim(),
            IsFeatured = false,
            Status = "pending"
        };

        var created = await _feedbackService.CreateFeedbackAsync(feedback);

        return Ok(new
        {
            success = true,
            message = "Cảm ơn bạn đã gửi cảm nhận! Đánh giá của bạn đã được gửi tới ban quản trị và sẽ hiển thị trên HomePlant sau khi được duyệt 🌿",
            data = new
            {
                id = created.Id,
                userName = created.UserName,
                userSubtitle = created.UserSubtitle,
                tag = created.Tag,
                tagIcon = created.TagIcon,
                tagType = created.TagType,
                rating = created.Rating,
                title = created.Title,
                content = created.Content,
                status = created.Status,
                createdAt = DateTime.UtcNow.ToString("dd/MM/yyyy")
            }
        });
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
