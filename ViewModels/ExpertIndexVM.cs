using System.ComponentModel.DataAnnotations;
using HomePlant.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomePlant.ViewModels;

public sealed class ExpertIndexVM
{
    [Display(Name = "Cây trong vườn")]
    public string? PlantId { get; set; }

    [Required(ErrorMessage = "Vui lòng mô tả điều bạn muốn hỏi.")]
    [StringLength(1500, MinimumLength = 5, ErrorMessage = "Câu hỏi cần từ 5 đến 1.500 ký tự.")]
    [Display(Name = "Câu hỏi hoặc triệu chứng")]
    public string Question { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn một ảnh cây.")]
    [Display(Name = "Ảnh cây")]
    public IFormFile? Photo { get; set; }

    public bool ConsentToAiProcessing { get; set; }

    public bool IsAiConfigured { get; set; }
    public List<SelectListItem> PlantOptions { get; set; } = new();
    public List<AiDiagnosisModel> History { get; set; } = new();
}
