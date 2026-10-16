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

            var summaryTask = RunReportSafe(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                metrics = new[] {
                    new { name = "activeUsers" },
                    new { name = "sessions" },
                    new { name = "screenPageViews" },
                    new { name = "userEngagementDuration" }
                }
            }, cancellationToken);

            var sourcesTask = RunReportSafe(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "sessionDefaultChannelGroup" } },
                metrics = new[] { new { name = "sessions" } },
                orderBys = new[] { new { metric = new { metricName = "sessions" }, desc = true } },
                limit = 5
            }, cancellationToken);

            var pagesTask = RunReportSafe(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "pagePath" }, new { name = "pageTitle" } },
                metrics = new[] {
                    new { name = "screenPageViews" },
                    new { name = "activeUsers" },
                    new { name = "userEngagementDuration" }
                },
                orderBys = new[] { new { metric = new { metricName = "screenPageViews" }, desc = true } },
                limit = 60
            }, cancellationToken);

            var dailyTask = RunReportSafe(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "date" } },
                metrics = new[] {
                    new { name = "activeUsers" },
                    new { name = "sessions" },
                    new { name = "screenPageViews" },
                    new { name = "newUsers" }
                },
                orderBys = new[] { new { dimension = new { dimensionName = "date" }, desc = false } }
            }, cancellationToken);

            var retentionTask = RunReportSafe(token, new
            {
                dateRanges = new[] { new { startDate = "30daysAgo", endDate = "today" } },
                dimensions = new[] { new { name = "newVsReturning" } },
                metrics = new[] { new { name = "activeUsers" } }
            }, cancellationToken);

            await Task.WhenAll(summaryTask, sourcesTask, pagesTask, dailyTask, retentionTask);

            var summaryDoc = summaryTask.Result;
            if (summaryDoc == null)
            {
                return new GoogleAnalyticsOverviewVM
                {
                    Configured = true,
                    Error = "GA4 chưa trả dữ liệu. Kiểm tra quyền Viewer của service account và Analytics Data API."
                };
            }

            var summary = summaryDoc.RootElement;
            var metricValues = summary.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0
                ? rows[0].GetProperty("metricValues") : default;

            var activeUsers = Metric(metricValues, 0);
            var sessions = Metric(metricValues, 1);
            var pageViews = Metric(metricValues, 2);
            var totalDuration = MetricDouble(metricValues, 3);
            var avgEngagement = activeUsers > 0 ? totalDuration / activeUsers : 0;

            var allPages = PageRows(pagesTask.Result?.RootElement);
            var popularPages = allPages.Take(10).ToList();
            var leastPages = allPages.Where(p => p.Views > 0).OrderBy(p => p.Views).Take(5).ToList();

            var dailyTrends = DailyRows(dailyTask.Result?.RootElement);
            var (newUsers, returningUsers) = RetentionUsers(retentionTask.Result?.RootElement);

            var last3Days = dailyTrends.TakeLast(3).ToList();
            double avgDailyUsers = last3Days.Count > 0 ? (double)last3Days.Sum(d => d.ActiveUsers) / last3Days.Count : 0;
            double avgDailySessions = last3Days.Count > 0 ? (double)last3Days.Sum(d => d.Sessions) / last3Days.Count : 0;
            double avgDailyPageViews = last3Days.Count > 0 ? (double)last3Days.Sum(d => d.PageViews) / last3Days.Count : 0;

            return new GoogleAnalyticsOverviewVM
            {
                Configured = true,
                Available = true,
                ActiveUsers30Days = activeUsers,
                Sessions30Days = sessions,
                PageViews30Days = pageViews,
                NewUsers30Days = newUsers,
                ReturningUsers30Days = returningUsers,
                AverageDailyUsers3Days = avgDailyUsers,
                AverageDailySessions3Days = avgDailySessions,
                AverageDailyPageViews3Days = avgDailyPageViews,
                AverageEngagementSeconds = avgEngagement,
                TrafficSources = Rows(sourcesTask.Result?.RootElement, 1, 1),
                PopularPages = popularPages,
                LeastVisitedPages = leastPages,
                DailyTrends = dailyTrends
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to load the GA4 report for property {PropertyId}.", _propertyId);
            return new GoogleAnalyticsOverviewVM { Configured = true, Error = "GA4 chưa trả dữ liệu. Kiểm tra quyền Viewer của service account và Analytics Data API." };
        }
    }

    private async Task<JsonDocument?> RunReportSafe(string token, object body, CancellationToken cancellationToken)
    {
        try
        {
            return await RunReport(token, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GA4 query failed for property {PropertyId}.", _propertyId);
            return null;
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

    private static double MetricDouble(JsonElement values, int index) =>
        values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > index &&
        double.TryParse(values[index].GetProperty("value").GetString(), System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static IReadOnlyList<AnalyticsRowVM> Rows(JsonElement? root, int dimensionCount, int metricIndex)
    {
        if (root == null || !root.Value.TryGetProperty("rows", out var rows)) return [];
        return rows.EnumerateArray().Select(row =>
        {
            var dimensions = row.GetProperty("dimensionValues");
            var label = string.Join(" · ", Enumerable.Range(0, dimensionCount)
                .Select(i => dimensions[i].GetProperty("value").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
            return new AnalyticsRowVM(label, Metric(row.GetProperty("metricValues"), metricIndex - 1));
        }).ToList();
    }

    private static IReadOnlyList<AnalyticsDailyRowVM> DailyRows(JsonElement? root)
    {
        if (root == null || !root.Value.TryGetProperty("rows", out var rows)) return [];
        var list = new List<AnalyticsDailyRowVM>();
        foreach (var row in rows.EnumerateArray())
        {
            var rawDate = row.GetProperty("dimensionValues")[0].GetProperty("value").GetString() ?? "";
            var displayDate = rawDate.Length == 8 && DateTime.TryParseExact(rawDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dt)
                ? dt.ToString("dd/MM")
                : rawDate;
            var metricValues = row.GetProperty("metricValues");
            var active = Metric(metricValues, 0);
            var sessions = Metric(metricValues, 1);
            var views = Metric(metricValues, 2);
            var newUsers = Metric(metricValues, 3);
            var returningUsers = Math.Max(0, active - newUsers);
            list.Add(new AnalyticsDailyRowVM(
                rawDate,
                displayDate,
                active,
                sessions,
                views,
                newUsers,
                returningUsers
            ));
        }
        return list;
    }

    private static IReadOnlyList<AnalyticsPageDetailVM> PageRows(JsonElement? root)
    {
        if (root == null || !root.Value.TryGetProperty("rows", out var rows)) return [];
        var list = new List<AnalyticsPageDetailVM>();
        foreach (var row in rows.EnumerateArray())
        {
            var dimensions = row.GetProperty("dimensionValues");
            var path = dimensions[0].GetProperty("value").GetString() ?? "/";
            var title = dimensions[1].GetProperty("value").GetString() ?? "";

            // Bỏ các trang của admin quản trị đi, chỉ giữ lại các trang user dùng
            if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Revenue", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var metricValues = row.GetProperty("metricValues");
            var views = Metric(metricValues, 0);
            var users = Metric(metricValues, 1);
            var duration = MetricDouble(metricValues, 2);
            list.Add(new AnalyticsPageDetailVM(path, title, views, users, duration));
        }
        return list;
    }

    private static (long newUsers, long returningUsers) RetentionUsers(JsonElement? root)
    {
        if (root == null || !root.Value.TryGetProperty("rows", out var rows)) return (0, 0);
        long newUsers = 0;
        long returningUsers = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var dim = row.GetProperty("dimensionValues")[0].GetProperty("value").GetString()?.ToLowerInvariant();
            var val = Metric(row.GetProperty("metricValues"), 0);
            if (dim == "new") newUsers = val;
            else if (dim == "returning") returningUsers = val;
        }
        return (newUsers, returningUsers);
    }
}
