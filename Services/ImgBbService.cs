using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HomePlant.Services;

public class ImgBbService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    public ImgBbService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["ImgBB:ApiKey"] ?? "";
    }

    public async Task<string?> Upload(IFormFile? photo)
    {
        if (photo == null || photo.Length == 0)
            return null;

        using var ms = new MemoryStream();
        await photo.CopyToAsync(ms);
        var base64Image = Convert.ToBase64String(ms.ToArray());

        var http = _httpClientFactory.CreateClient();

        using var content = new MultipartFormDataContent
        {
            { new StringContent(_apiKey), "key" },
            { new StringContent(base64Image), "image" }
        };

        var response = await http.PostAsync(
            "https://api.imgbb.com/1/upload",
            content);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content
            .ReadFromJsonAsync<ImgBbResponse>();

        return result?.Data?.Url;
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
