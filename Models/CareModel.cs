using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class CareModel
{
    [FirestoreProperty("light")]
    public string Light { get; set; }

    [FirestoreProperty("water")]
    public string Water { get; set; }

    [FirestoreProperty("soil")]
    public string Soil { get; set; }

    [FirestoreProperty("fertilizer")]
    public string Fertilizer { get; set; }
}