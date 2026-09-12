using HomePlant.Models;

namespace HomePlant.ViewModels;

public class PlantDetailsVM
{
    public UserPlantModel Plant { get; set; } = new();
    public PlantSpeciesVM? Species { get; set; }
    public List<CareLogModel> CareLogs { get; set; } = new();
    public CareScheduleVM Schedule { get; set; } = new();
}
