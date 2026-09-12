using HomePlant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

var passed = 0;

async Task Check(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

ImgBbService Service(HttpMessageHandler handler, string apiKey = "test-key") => new(
    new FakeFactory(handler),
    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ImgBB:ApiKey"] = apiKey
    }).Build(),
    NullLogger<ImgBbService>.Instance);

IFormFile FormFile(long length)
{
    var stream = new MemoryStream(new byte[length]);
    return new FormFile(stream, 0, length, "Photo", "plant.png")
    {
        Headers = new HeaderDictionary(),
        ContentType = "image/png"
    };
}

await Check("empty upload does not call ImgBB", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new Exception("HTTP must not run")));
    if (await service.Upload(null) != null) throw new Exception("Expected no URL");
});

await Check("oversized upload is skipped", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new Exception("HTTP must not run")));
    if (await service.Upload(FormFile(ImgBbService.MaxImageBytes + 1)) != null) throw new Exception("Expected no URL");
});

await Check("network failure returns no URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new HttpRequestException("offline")));
    if (await service.Upload(FormFile(1)) != null) throw new Exception("Expected no URL");
});

await Check("timeout returns no URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new TaskCanceledException("timeout")));
    if (await service.Upload(FormFile(1)) != null) throw new Exception("Expected no URL");
});

await Check("successful upload returns provider URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"data\":{\"url\":\"https://example.invalid/plant.png\"}}")
    })));
    var url = await service.Upload(FormFile(1));
    if (url != "https://example.invalid/plant.png") throw new Exception("Unexpected URL");
});

await Check("HTTP 400 retries with a base64 image", async () =>
{
    var attempts = 0;
    var service = Service(new FakeHandler(async (request, cancellationToken) =>
    {
        attempts++;
        var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

        if (attempts == 1)
        {
            if (requestBody.Contains(Convert.ToBase64String(new byte[] { 0x2a })))
                throw new Exception("The first attempt should use binary multipart data");
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"code\":100,\"message\":\"Invalid image source\"}}")
            };
        }

        if (!requestBody.Contains(Convert.ToBase64String(new byte[] { 0x2a })))
            throw new Exception("The retry should contain a base64 image");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"url\":\"https://example.invalid/retried.png\"}}")
        };
    }));

    var url = await service.Upload(FormFileWithBytes(new byte[] { 0x2a }));
    if (url != "https://example.invalid/retried.png" || attempts != 2)
        throw new Exception("Expected a successful base64 retry");
});

await Check("non-JSON HTTP 400 still retries", async () =>
{
    var attempts = 0;
    var service = Service(new FakeHandler((_, _) =>
    {
        attempts++;
        return Task.FromResult(new HttpResponseMessage(
            attempts == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
        {
            Content = new StringContent(attempts == 1
                ? "upstream error"
                : "{\"data\":{\"url\":\"https://example.invalid/recovered.png\"}}")
        });
    }));

    var url = await service.Upload(FormFileWithBytes(new byte[] { 0x2a }));
    if (url != "https://example.invalid/recovered.png" || attempts != 2)
        throw new Exception("Expected retry after a non-JSON rejection");
});

if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    await Check("real ImgBB accepts a binary image", async () =>
    {
        var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        using var settings = System.Text.Json.JsonDocument.Parse(
            await System.IO.File.ReadAllTextAsync(settingsPath));
        var apiKey = settings.RootElement
            .GetProperty("ImgBB")
            .GetProperty("ApiKey")
            .GetString() ?? "";

        var suppliedPath = args.SkipWhile(arg => arg != "--live").Skip(1).FirstOrDefault();
        var imageBytes = suppliedPath == null
            // Valid 1x1 transparent PNG; keeps the default live check deterministic and non-sensitive.
            ? Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")
            : await System.IO.File.ReadAllBytesAsync(suppliedPath);
        var fileName = suppliedPath == null ? "smoke-test.png" : Path.GetFileName(suppliedPath);
        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
        using var stream = new MemoryStream(imageBytes);
        var image = new FormFile(stream, 0, imageBytes.Length, "Photo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        var url = await Service(new HttpClientHandler(), apiKey).Upload(image);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uploaded) || uploaded.Scheme != Uri.UriSchemeHttps)
            throw new Exception("ImgBB did not return an HTTPS image URL");
    });
}

Console.WriteLine($"{passed} image upload checks passed.");

IFormFile FormFileWithBytes(byte[] bytes)
{
    var stream = new MemoryStream(bytes);
    return new FormFile(stream, 0, bytes.Length, "Photo", "plant.png")
    {
        Headers = new HeaderDictionary(),
        ContentType = "image/png"
    };
}

sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);
}
