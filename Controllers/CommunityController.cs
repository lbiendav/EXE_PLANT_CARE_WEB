using Microsoft.AspNetCore.Mvc;

using HomePlant.Services;

namespace HomePlant.Controllers;

public sealed class CommunityController(
    CommunityPostService posts,
    IConfiguration configuration,
    ILogger<CommunityController> logger) : Controller
{
    [HttpGet("/Community")]
    public async Task<IActionResult> Index()
    {
        const string defaultGroupUrl = "https://www.facebook.com/share/g/1BncNcnzpy/";
        var configured = configuration["Community:FacebookGroupUrl"] ?? defaultGroupUrl;
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
            (uri.Host.Equals("facebook.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.facebook.com", StringComparison.OrdinalIgnoreCase)) &&
            (uri.AbsolutePath.StartsWith("/groups/", StringComparison.OrdinalIgnoreCase) ||
             uri.AbsolutePath.StartsWith("/share/g/", StringComparison.OrdinalIgnoreCase)))
            ViewBag.FacebookGroupUrl = uri.AbsoluteUri;
        else
            logger.LogError("Community Facebook group URL is invalid; hiding the Facebook CTA.");

        return View((await posts.GetAll()).Where(post => post.Status == "active").ToList());
    }
}
