using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class ProfileVM
{
    public string? Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; set; }

    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Số điện thoại phải gồm đúng 8 chữ số.")]
    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }
}
