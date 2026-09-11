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

IFormFile File(long length)
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
    if (await service.Upload(File(ImgBbService.MaxImageBytes + 1)) != null) throw new Exception("Expected no URL");
});

await Check("network failure returns no URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new HttpRequestException("offline")));
    if (await service.Upload(File(1)) != null) throw new Exception("Expected no URL");
});

await Check("timeout returns no URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => throw new TaskCanceledException("timeout")));
    if (await service.Upload(File(1)) != null) throw new Exception("Expected no URL");
});

await Check("successful upload returns provider URL", async () =>
{
    var service = Service(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"data\":{\"url\":\"https://example.invalid/plant.png\"}}")
    })));
    var url = await service.Upload(File(1));
    if (url != "https://example.invalid/plant.png") throw new Exception("Unexpected URL");
});

Console.WriteLine($"{passed} image upload checks passed; no real ImgBB calls.");

sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);
}
