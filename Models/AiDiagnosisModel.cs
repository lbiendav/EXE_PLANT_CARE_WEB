using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class AiDiagnosisResultModel
{
    [FirestoreProperty("identifiedPlant")]
    public string IdentifiedPlant { get; set; } = "";

    [FirestoreProperty("diseaseName")]
    public string DiseaseName { get; set; } = "";

    [FirestoreProperty("confidence")]
    public double Confidence { get; set; }

    [FirestoreProperty("cause")]
    public string Cause { get; set; } = "";

    [FirestoreProperty("treatment")]
    public string Treatment { get; set; } = "";

    [FirestoreProperty("summary")]
    public string Summary { get; set; } = "";

    [FirestoreProperty("observations")]
    public List<string> Observations { get; set; } = new();

    [FirestoreProperty("immediateActions")]
    public List<string> ImmediateActions { get; set; } = new();

    [FirestoreProperty("sevenDayPlan")]
    public List<string> SevenDayPlan { get; set; } = new();

    [FirestoreProperty("warnings")]
    public List<string> Warnings { get; set; } = new();

    [FirestoreProperty("needsMoreInfo")]
    public bool NeedsMoreInfo { get; set; }

    [FirestoreProperty("followUpQuestion")]
    public string FollowUpQuestion { get; set; } = "";

    [FirestoreProperty("careRecommendations")]
    public AiCareRecommendationsModel CareRecommendations { get; set; } = new();
}

[FirestoreData]
public class AiCareRecommendationsModel
{
    [FirestoreProperty("isSuitableForAutomation")]
    public bool IsSuitableForAutomation { get; set; }

    [FirestoreProperty("generalNote")]
    public string GeneralNote { get; set; } = "";

    [FirestoreProperty("watering")]
    public AiCareFrequencyModel Watering { get; set; } = new();

    [FirestoreProperty("fertilizing")]
    public AiCareFrequencyModel Fertilizing { get; set; } = new();

    [FirestoreProperty("repotting")]
    public AiCareFrequencyModel Repotting { get; set; } = new();
}

[FirestoreData]
public class AiCareFrequencyModel
{
    [FirestoreProperty("frequency")]
    public int Frequency { get; set; }

    [FirestoreProperty("unit")]
    public string Unit { get; set; } = "Days";

    [FirestoreProperty("reason")]
    public string Reason { get; set; } = "";
}

[FirestoreData]
public class AiDiagnosisModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("diagnosisId")]
    public string DiagnosisId { get; set; } = "";

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = "";

    [FirestoreProperty("uploadedImageUrl")]
    public string UploadedImageUrl { get; set; } = "";

    [FirestoreProperty("plantId")]
    public string PlantId { get; set; } = "";

    [FirestoreProperty("plantName")]
    public string PlantName { get; set; } = "";

    [FirestoreProperty("question")]
    public string Question { get; set; } = "";

    [FirestoreProperty("aiModel")]
    public string AiModel { get; set; } = "";

    [FirestoreProperty("result")]
    public AiDiagnosisResultModel Result { get; set; } = new();

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("careRecommendationsAppliedAt")]
    public Timestamp? CareRecommendationsAppliedAt { get; set; }
}
