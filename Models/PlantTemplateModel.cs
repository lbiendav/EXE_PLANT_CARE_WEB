using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class PlantTemplateModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("templateId")]
    public string TemplateId { get; set; }

    [FirestoreProperty("name")]
    public string Name { get; set; }

    [FirestoreProperty("scientificName")]
    public string ScientificName { get; set; }

    [FirestoreProperty("description")]
    public string Description { get; set; }

    [FirestoreProperty("imageUrl")]
    public string ImageUrl { get; set; }

    [FirestoreProperty("careInstructions")]
    public CareModel CareInstructions { get; set; }

    [FirestoreProperty("isFeatured")]
    public bool IsFeatured { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }
}
