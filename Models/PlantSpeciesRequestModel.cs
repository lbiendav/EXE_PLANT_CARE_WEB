using Google.Cloud.Firestore;

namespace HomePlant.Models;

[FirestoreData]
public class PlantSpeciesRequestModel
{
    [FirestoreDocumentId]
    public string Id { get; set; } = "";

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = "";

    [FirestoreProperty("userEmail")]
    public string UserEmail { get; set; } = "";

    [FirestoreProperty("userDisplayName")]
    public string UserDisplayName { get; set; } = "";

    [FirestoreProperty("userPlantId")]
    public string UserPlantId { get; set; } = "";

    [FirestoreProperty("plantCustomName")]
    public string PlantCustomName { get; set; } = "";

    [FirestoreProperty("proposedName")]
    public string ProposedName { get; set; } = "";

    [FirestoreProperty("scientificName")]
    public string ScientificName { get; set; } = "";

    [FirestoreProperty("description")]
    public string Description { get; set; } = "";

    [FirestoreProperty("imageUrl")]
    public string ImageUrl { get; set; } = "";

    [FirestoreProperty("light")]
    public string Light { get; set; } = "";

    [FirestoreProperty("water")]
    public string Water { get; set; } = "";

    [FirestoreProperty("soil")]
    public string Soil { get; set; } = "";

    [FirestoreProperty("fertilizer")]
    public string Fertilizer { get; set; } = "";

    [FirestoreProperty("status")]
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected"

    [FirestoreProperty("createdAt")]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public Timestamp? UpdatedAt { get; set; }

    [FirestoreProperty("approvedSampleId")]
    public string? ApprovedSampleId { get; set; }

    public string DisplayStatus => Status switch
    {
        "pending" => "Đang chờ duyệt",
        "approved" => "Đã duyệt",
        "rejected" => "Đã từ chối",
        _ => Status
    };

    public string StatusColorClass => Status switch
    {
        "pending" => "warning",
        "approved" => "success",
        "rejected" => "danger",
        _ => "secondary"
    };
}
