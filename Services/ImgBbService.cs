using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomePlant.Services;

public class ImgBbService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImgBbService> _logger;
    private readonly string _apiKey;

    // ImgBB API v1 accepts an image payload up to 32 MB.
    public const long MaxImageBytes = 32 * 1024 * 1024;

    public ImgBbService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ImgBbService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["ImgBB:ApiKey"] ?? "";
    }

    public async Task<string?> Upload(
        IFormFile? photo,
        CancellationToken cancellationToken = default)
    {
        if (photo == null || photo.Length == 0)
            return null;

        if (photo.Length > MaxImageBytes)
        {
            _logger.LogWarning("Image upload skipped because the file exceeds {MaxImageBytes} bytes.", MaxImageBytes);
            return null;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("Image upload skipped because ImgBB is not configured.");
            return null;
        }

        try
        {
            var http = _httpClientFactory.CreateClient(nameof(ImgBbService));

            await using var imageStream = photo.OpenReadStream();
            using var imageContent = new StreamContent(imageStream);
            if (MediaTypeHeaderValue.TryParse(photo.ContentType, out var contentType))
                imageContent.Headers.ContentType = contentType;

            using var content = new MultipartFormDataContent
            {
                { new StringContent(_apiKey), "key" },
                { imageContent, "image", Path.GetFileName(photo.FileName) }
            };

            using var response = await http.PostAsync(
                "https://api.imgbb.com/1/upload",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ImgBB rejected an image upload with HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<ImgBbResponse>(cancellationToken: cancellationToken);

            return result?.Data?.Url;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ImgBB image upload timed out.");
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "ImgBB image upload failed.");
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "ImgBB returned an invalid image upload response.");
            return null;
        }
    }

    private class ImgBbResponse
    {
        [JsonPropertyName("data")]
        public ImgBbData? Data { get; set; }
    }

    private class ImgBbData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
