using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

public sealed class CommunityController(IConfiguration configuration, ILogger<CommunityController> logger) : Controller
{
    [HttpGet("/Community")]
    public IActionResult Index()
    {
        var configured = configuration["Community:FacebookGroupUrl"];
        if (string.IsNullOrWhiteSpace(configured)) return View();
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
            (uri.Host.Equals("facebook.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.facebook.com", StringComparison.OrdinalIgnoreCase)) &&
            uri.AbsolutePath.StartsWith("/groups/", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Length > "/groups/".Length)
            return Redirect(uri.AbsoluteUri);

        logger.LogError("Community Facebook group URL is invalid; showing the coming-soon page.");
        return View();
    }
}
