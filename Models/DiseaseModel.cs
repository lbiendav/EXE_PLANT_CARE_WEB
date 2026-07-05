using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class DiseaseModel
{
    [FirestoreProperty("issue")]
    public string Issue { get; set; }

    [FirestoreProperty("cause")]
    public string Cause { get; set; }

    [FirestoreProperty("treatment")]
    public string Treatment { get; set; }
}