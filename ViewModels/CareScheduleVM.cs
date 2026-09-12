using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class CareScheduleVM : IValidatableObject
{
    [Range(1, 3650, ErrorMessage = "Chu kỳ tưới phải từ 1 đến 3650")]
    public int? WateringFrequency { get; set; }
    public string WateringFrequencyUnit { get; set; } = "Days";

    [Range(1, 3650, ErrorMessage = "Chu kỳ bón phân phải từ 1 đến 3650")]
    public int? FertilizingFrequency { get; set; }
    public string FertilizingFrequencyUnit { get; set; } = "Days";

    [Range(1, 3650, ErrorMessage = "Chu kỳ thay chậu phải từ 1 đến 3650")]
    public int? RepottingFrequency { get; set; }
    public string RepottingFrequencyUnit { get; set; } = "Days";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var item in new[]
        {
            (Value: WateringFrequency, Unit: WateringFrequencyUnit, Field: nameof(WateringFrequencyUnit)),
            (Value: FertilizingFrequency, Unit: FertilizingFrequencyUnit, Field: nameof(FertilizingFrequencyUnit)),
            (Value: RepottingFrequency, Unit: RepottingFrequencyUnit, Field: nameof(RepottingFrequencyUnit))
        })
        {
            if (item.Value.HasValue && item.Unit is not ("Seconds" or "Minutes" or "Hours" or "Days"))
                yield return new ValidationResult("Đơn vị thời gian không hợp lệ.", new[] { item.Field });
        }
    }
}
