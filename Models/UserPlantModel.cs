using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class UserPlantModel
{
    [FirestoreDocumentId]
    public string Id { get; set; }

    [FirestoreProperty("plantId")]
    public string PlantId { get; set; }

    [FirestoreProperty("templateId")]
    public string TemplateId { get; set; }

    [FirestoreProperty("customName")]
    public string CustomName { get; set; }

    [FirestoreProperty("imageUrl")]
    public string ImageUrl { get; set; }

    [FirestoreProperty("status")]
    public string Status { get; set; }

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("plantedAt")]
    public Timestamp PlantedAt { get; set; }

    [FirestoreProperty("lastWatered")]
    public Timestamp? LastWatered { get; set; }

    [FirestoreProperty("lastFertilized")]
    public Timestamp? LastFertilized { get; set; }

    [FirestoreProperty("lastRepotted")]
    public Timestamp? LastRepotted { get; set; }

    [FirestoreProperty("wateringFrequency")]
    public int? WateringFrequency { get; set; }

    [FirestoreProperty("fertilizingFrequency")]
    public int? FertilizingFrequency { get; set; }

    [FirestoreProperty("repottingFrequency")]
    public int? RepottingFrequency { get; set; }

    public string DisplayStatus => Status switch
    {
        "healthy" => "Khỏe mạnh",
        "warning" => "Cần chú ý",
        "sick" => "Bị bệnh",
        _ => Status
    };

    public string StatusColorClass => Status switch
    {
        "healthy" => "success",
        "warning" => "warning",
        "sick" => "danger",
        _ => "secondary"
    };
}
