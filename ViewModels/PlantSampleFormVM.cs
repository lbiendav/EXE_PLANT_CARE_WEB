using System.ComponentModel.DataAnnotations;
using HomePlant.Models;

namespace HomePlant.ViewModels;

public class PlantSampleFormVM
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên cây")]
    public string Name { get; set; }

    public string? ScientificName { get; set; }

    public string? Description { get; set; }

    public IFormFile? Photo { get; set; }

    public string? ExistingImageUrl { get; set; }

    public string? Light { get; set; }

    public string? Water { get; set; }

    public string? Soil { get; set; }

    public string? Fertilizer { get; set; }

    public List<DiseaseModel> Diseases { get; set; } = new();
}
