using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using HomePlant.ViewModels;

namespace HomePlant.Services;

public sealed class GoogleAnalyticsService(
    HttpClient http,
    GoogleCredential credential,
    IConfiguration configuration,
    ILogger<GoogleAnalyticsService> logger)
{
    private const string AnalyticsScope = "https://www.googleapis.com/auth/analytics.readonly";
    private readonly string _propertyId = configuration["GoogleAnalytics:PropertyId"]?.Trim() ?? "";

    public bool IsConfigured => long.TryParse(_propertyId, out _);

    public async Task<GoogleAnalyticsOverviewVM> GetOverview(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return GoogleAnalyticsOverviewVM.NotConfigured;
        try
        {
            var scoped = credential.IsCreateScopedRequired ? credential.CreateScoped(AnalyticsScope) : credential;
            var token = await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
            var summaryTask = RunReport(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                metrics = new[] { new { name = "activeUsers" }, new { name = "sessions" }, new { name = "screenPageViews" } }
            }, cancellationToken);
            var sourcesTask = RunReport(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "sessionDefaultChannelGroup" } },
                metrics = new[] { new { name = "sessions" } }, orderBys = new[] { new { metric = new { metricName = "sessions" }, desc = true } }, limit = 5
            }, cancellationToken);
            var pagesTask = RunReport(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "pageTitle" }, new { name = "pagePath" } },
                metrics = new[] { new { name = "screenPageViews" } }, orderBys = new[] { new { metric = new { metricName = "screenPageViews" }, desc = true } }, limit = 5
            }, cancellationToken);
            await Task.WhenAll(summaryTask, sourcesTask, pagesTask);

            var summary = summaryTask.Result.RootElement;
            var metricValues = summary.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0
                ? rows[0].GetProperty("metricValues") : default;
            return new GoogleAnalyticsOverviewVM
            {
                Configured = true,
                Available = true,
                ActiveUsers30Days = Metric(metricValues, 0),
                Sessions30Days = Metric(metricValues, 1),
                PageViews30Days = Metric(metricValues, 2),
                TrafficSources = Rows(sourcesTask.Result.RootElement, 1, 1),
                PopularPages = Rows(pagesTask.Result.RootElement, 2, 1)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to load the GA4 report for property {PropertyId}.", _propertyId);
            return new GoogleAnalyticsOverviewVM { Configured = true, Error = "GA4 chưa trả dữ liệu. Kiểm tra quyền Viewer của service account và Analytics Data API." };
        }
    }

    private async Task<JsonDocument> RunReport(string token, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://analyticsdata.googleapis.com/v1beta/properties/{_propertyId}:runReport");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GA4 Data API returned {(int)response.StatusCode}.");
        return JsonDocument.Parse(payload);
    }

    private static long Metric(JsonElement values, int index) =>
        values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > index &&
        long.TryParse(values[index].GetProperty("value").GetString(), out var value) ? value : 0;

    private static IReadOnlyList<AnalyticsRowVM> Rows(JsonElement root, int dimensionCount, int metricIndex)
    {
        if (!root.TryGetProperty("rows", out var rows)) return [];
        return rows.EnumerateArray().Select(row =>
        {
            var dimensions = row.GetProperty("dimensionValues");
            var label = string.Join(" · ", Enumerable.Range(0, dimensionCount)
                .Select(i => dimensions[i].GetProperty("value").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
            return new AnalyticsRowVM(label, Metric(row.GetProperty("metricValues"), metricIndex - 1));
        }).ToList();
    }
}
