using HomePlant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomePlant.Controllers;

[Route("Image")]
public sealed class ImageController : Controller
{
    private readonly ImageStorageService _images;

    public ImageController(ImageStorageService images)
    {
        _images = images;
    }

    [HttpGet("{id}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string id)
    {
        var image = await _images.Get(id, HttpContext.RequestAborted);
        if (image == null)
            return NotFound();

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(image.Bytes, image.ContentType);
    }
}
