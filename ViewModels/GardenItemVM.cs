using HomePlant.Models;

namespace HomePlant.ViewModels;

public class GardenItemVM
{
    public UserPlantModel Plant { get; set; }

    public PlantTemplateModel? Template { get; set; }
}
