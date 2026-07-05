using Google.Cloud.Firestore;
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

        return View(articles);
    }

    public async Task<IActionResult> Details(
        string id)
    {
        var article =
            await _service.GetById(id);

        return View(article);
    }

    public async Task<IActionResult> Delete(
        string id)
    {
        await _service.Delete(id);

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        ArticleModel article)
    {
        article.CreatedAt =
            Timestamp.GetCurrentTimestamp();

        await _service.Add(article);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(
    string id)
    {
        var article =
            await _service.GetById(id);

        return View(article);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        string id,
        ArticleModel article)
    {
        await _service.Update(id, article);

        return RedirectToAction(nameof(Index));
    }
}