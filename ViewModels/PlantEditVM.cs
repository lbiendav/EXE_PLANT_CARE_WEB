using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class PlantEditVM
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên gọi")]
    public string Nickname { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn loại cây")]
    public string PlantSampleId { get; set; } = "";

    public string CurrentStatus { get; set; } = "Khỏe mạnh";

    [Range(1, 3650, ErrorMessage = "Tần suất tưới phải từ 1 đến 3650 ngày")]
    public int? WateringFrequency { get; set; }

    [Range(1, 3650, ErrorMessage = "Tần suất bón phân phải từ 1 đến 3650 ngày")]
    public int? FertilizingFrequency { get; set; }

    [Range(1, 3650, ErrorMessage = "Tần suất thay chậu phải từ 1 đến 3650 ngày")]
    public int? RepottingFrequency { get; set; }

    public string ExistingImageUrl { get; set; } = "";

    public IFormFile? Photo { get; set; }
}
