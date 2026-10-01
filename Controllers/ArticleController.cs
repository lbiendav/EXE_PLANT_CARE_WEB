using Google.Cloud.Firestore;
using HomePlant.Filters;
using HomePlant.Models;
using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public class ArticleController : Controller
{
    private readonly ArticleService _service;

    public ArticleController(
        ArticleService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var articles =
            await _service.GetAll();

        ViewBag.IsAdmin = new[] { "admin", "super_admin", "content_admin" }.Contains(
            HttpContext.Session.GetString("Role") ?? "",
            StringComparer.OrdinalIgnoreCase);

        return View(articles);
    }

    public async Task<IActionResult> Details(
        string id)
    {
        var article =
            await _service.GetByIdAndIncrementViews(id);

        if (article == null) return NotFound();
        return View(article);
    }

    [AdminOnly]
    [HttpPost]
    public async Task<IActionResult> Delete(
        string id)
    {
        await _service.Delete(id);

        return RedirectToAction(nameof(Index));
    }

    [AdminOnly]
    public IActionResult Create()
    {
        return View();
    }

    [AdminOnly]
    [HttpPost]
    public async Task<IActionResult> Create(
        ArticleModel article)
    {
        article.Title = article.Title.Trim();
        article.Content = article.Content.Trim();
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Vui lòng nhập tiêu đề và nội dung bài viết.");
            return View(article);
        }
        article.CoverImage ??= "";
        article.Views = 0;
        article.CreatedAt =
            Timestamp.GetCurrentTimestamp();

        await _service.Add(article);

        return RedirectToAction(nameof(Index));
    }

    [AdminOnly]
    public async Task<IActionResult> Edit(
    string id)
    {
        var article =
            await _service.GetById(id);

        if (article == null) return NotFound();
        return View(article);
    }

    [AdminOnly]
    [HttpPost]
    public async Task<IActionResult> Edit(
        string id,
        ArticleModel article)
    {
        var existing = await _service.GetById(id);
        if (existing == null) return NotFound();
        article.Title = article.Title.Trim();
        article.Content = article.Content.Trim();
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Vui lòng nhập tiêu đề và nội dung bài viết.");
            return View(article);
        }
        article.CreatedAt = existing.CreatedAt;
        article.Views = existing.Views;
        article.Tags = existing.Tags;
        article.CoverImage ??= "";
        await _service.Update(id, article);

        return RedirectToAction(nameof(Index));
    }
}
