using System.ComponentModel.DataAnnotations;
using HomePlant.Models;

namespace HomePlant.ViewModels;

public class PlantSampleFormVM
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên cây")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(120)]
    public string? ScientificName { get; set; }

    [StringLength(5_000)]
    public string? Description { get; set; }

    public IFormFile? Photo { get; set; }

    [StringLength(2048)]
    public string? ExistingImageUrl { get; set; }

    [StringLength(1_000)]
    public string? Light { get; set; }

    [StringLength(1_000)]
    public string? Water { get; set; }

    [StringLength(1_000)]
    public string? Soil { get; set; }

    [StringLength(1_000)]
    public string? Fertilizer { get; set; }

    public List<DiseaseModel> Diseases { get; set; } = new();
}
