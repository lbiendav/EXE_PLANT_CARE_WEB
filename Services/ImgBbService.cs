using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net;
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
            using var buffer = new MemoryStream((int)photo.Length);
            await imageStream.CopyToAsync(buffer, cancellationToken);
            var imageBytes = buffer.ToArray();

            var result = await Send(http, imageBytes, photo, encodeAsBase64: false, cancellationToken);
            if (result.Url != null)
                return result.Url;

            // ImgBB intermittently rejects binary multipart uploads from some
            // hosting networks with HTTP 400. Its API also accepts base64, so
            // retry that documented representation before giving up.
            if (result.StatusCode == HttpStatusCode.BadRequest)
            {
                _logger.LogInformation("Retrying the ImgBB upload using base64 encoding.");
                result = await Send(http, imageBytes, photo, encodeAsBase64: true, cancellationToken);
            }

            return result.Url;
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

    private async Task<UploadAttempt> Send(
        HttpClient http,
        byte[] imageBytes,
        IFormFile photo,
        bool encodeAsBase64,
        CancellationToken cancellationToken)
    {
        using HttpContent imageContent = encodeAsBase64
            ? new StringContent(Convert.ToBase64String(imageBytes))
            : new ByteArrayContent(imageBytes);

        if (!encodeAsBase64 && MediaTypeHeaderValue.TryParse(photo.ContentType, out var contentType))
            imageContent.Headers.ContentType = contentType;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(_apiKey), "key");
        if (encodeAsBase64)
            content.Add(imageContent, "image");
        else
            content.Add(imageContent, "image", Path.GetFileName(photo.FileName));

        using var response = await http.PostAsync(
            "https://api.imgbb.com/1/upload",
            content,
            cancellationToken);
        ImgBbResponse? result = null;
        try
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            result = JsonSerializer.Deserialize<ImgBbResponse>(responseBody);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "ImgBB returned a non-JSON upload response with HTTP {StatusCode}.", (int)response.StatusCode);
        }

        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(result?.Data?.Url))
            return new UploadAttempt(response.StatusCode, result.Data.Url);

        _logger.LogWarning(
            "ImgBB rejected an image upload with HTTP {StatusCode}, error {ErrorCode}: {ErrorMessage}",
            (int)response.StatusCode,
            result?.Error?.Code,
            result?.Error?.Message ?? "No error message returned");
        return new UploadAttempt(response.StatusCode, null);
    }

    private sealed record UploadAttempt(HttpStatusCode StatusCode, string? Url);

    private class ImgBbResponse
    {
        [JsonPropertyName("data")]
        public ImgBbData? Data { get; set; }

        [JsonPropertyName("error")]
        public ImgBbError? Error { get; set; }
    }

    private class ImgBbData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    private class ImgBbError
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
