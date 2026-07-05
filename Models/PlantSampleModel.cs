using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class PlantSampleModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("name")]
    public string Name { get; set; }

    [FirestoreProperty("scientificName")]
    public string ScientificName { get; set; }

    [FirestoreProperty("description")]
    public string Description { get; set; }

    [FirestoreProperty("imageUrl")]
    public string Image { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("care")]
    public CareModel Care { get; set; }

    [FirestoreProperty("diseases")]
    public List<DiseaseModel> Diseases { get; set; }
}