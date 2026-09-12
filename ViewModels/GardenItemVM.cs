using HomePlant.Models;

namespace HomePlant.ViewModels;

public class GardenItemVM
{
    public UserPlantModel Plant { get; set; } = new();

    public PlantSpeciesVM? Species { get; set; }

    public (string Label, Google.Cloud.Firestore.Timestamp DueAt)? NextCare
    {
        get
        {
            var candidates = new List<(string, Google.Cloud.Firestore.Timestamp)>();
            if (Plant.NextWateringAt.HasValue) candidates.Add(("Tưới nước", Plant.NextWateringAt.Value));
            if (Plant.NextFertilizingAt.HasValue) candidates.Add(("Bón phân", Plant.NextFertilizingAt.Value));
            if (Plant.NextRepottingAt.HasValue) candidates.Add(("Thay chậu", Plant.NextRepottingAt.Value));
            return candidates.Count == 0 ? null : candidates.OrderBy(x => x.Item2).First();
        }
    }
}
