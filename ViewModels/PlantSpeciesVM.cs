using HomePlant.Models;

namespace HomePlant.ViewModels;

public class PlantSpeciesVM
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ScientificName { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public CareModel? Care { get; set; }
    public bool IsLegacy { get; set; }

    public static PlantSpeciesVM FromSample(PlantSampleModel plant) => new()
    {
        Id = plant.Id,
        Name = plant.Name,
        ScientificName = plant.ScientificName,
        Description = plant.Description,
        ImageUrl = plant.Image,
        Care = plant.Care
    };

    public static PlantSpeciesVM FromTemplate(PlantTemplateModel plant) => new()
    {
        Id = plant.Id,
        Name = plant.Name,
        ScientificName = plant.ScientificName,
        Description = plant.Description,
        ImageUrl = plant.ImageUrl,
        Care = plant.CareInstructions,
        IsLegacy = true
    };
}
