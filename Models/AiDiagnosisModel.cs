using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class AiDiagnosisResultModel
{
    [FirestoreProperty("diseaseName")]
    public string DiseaseName { get; set; }

    [FirestoreProperty("confidence")]
    public double Confidence { get; set; }

    [FirestoreProperty("cause")]
    public string Cause { get; set; }

    [FirestoreProperty("treatment")]
    public string Treatment { get; set; }
}

[FirestoreData]
public class AiDiagnosisModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("diagnosisId")]
    public string DiagnosisId { get; set; }

    [FirestoreProperty("userId")]
    public string UserId { get; set; }

    [FirestoreProperty("uploadedImageUrl")]
    public string UploadedImageUrl { get; set; }

    [FirestoreProperty("result")]
    public AiDiagnosisResultModel Result { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
