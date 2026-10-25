namespace HomePlant.ViewModels;

public class AdminDashboardVM
{
    public int UserCount { get; set; }

    public int ArticleCount { get; set; }

    public int PlantTemplateCount { get; set; }

    public int SamplePlantCount { get; set; }

    public int GardenPlantCount { get; set; }

    public int CommunityPostCount { get; set; }

    public int QaThreadCount { get; set; }

    public int AiDiagnosisCount { get; set; }

    public int PendingSpeciesRequestCount { get; set; }

    public int PendingFeedbackCount { get; set; }

    public GoogleAnalyticsOverviewVM Analytics { get; set; } = GoogleAnalyticsOverviewVM.NotConfigured;
}
