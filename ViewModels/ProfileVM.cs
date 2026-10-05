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

    [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Số điện thoại phải gồm đúng 10 chữ số.")]
    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }
}
