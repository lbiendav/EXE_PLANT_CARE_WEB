using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class PlantCreateVM
{
    [Required(ErrorMessage = "Vui lòng nhập tên gọi")]
    public string Nickname { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại cây")]
    public string PlantSampleId { get; set; }

    public string CurrentStatus { get; set; } = "Khỏe mạnh";

    [Range(1, 3650, ErrorMessage = "Chu kỳ tưới phải từ 1 đến 3650")]
    public int? WateringFrequency { get; set; }

    public string WateringFrequencyUnit { get; set; } = "Days";

    [Range(1, 3650, ErrorMessage = "Chu kỳ bón phân phải từ 1 đến 3650")]
    public int? FertilizingFrequency { get; set; }

    public string FertilizingFrequencyUnit { get; set; } = "Days";

    [Range(1, 3650, ErrorMessage = "Chu kỳ thay chậu phải từ 1 đến 3650")]
    public int? RepottingFrequency { get; set; }

    public string RepottingFrequencyUnit { get; set; } = "Days";

    public IFormFile? Photo { get; set; }
}
