using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class PlantCreateVM
{
    [Required(ErrorMessage = "Vui lòng nhập tên gọi")]
    public string Nickname { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại cây")]
    public string PlantSampleId { get; set; }

    public string CurrentStatus { get; set; } = "Khỏe mạnh";

    public IFormFile? Photo { get; set; }
}
