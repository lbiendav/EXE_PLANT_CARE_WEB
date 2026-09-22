using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class PlantEditVM
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên gọi")]
    [StringLength(80, ErrorMessage = "Tên cây không được dài quá 80 ký tự")]
    public string Nickname { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn loại cây")]
    public string PlantSampleId { get; set; } = "";

    public string CurrentStatus { get; set; } = "Khỏe mạnh";

    public string ExistingImageUrl { get; set; } = "";

    public IFormFile? Photo { get; set; }
}
