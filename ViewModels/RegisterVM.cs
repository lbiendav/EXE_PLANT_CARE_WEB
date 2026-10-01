using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class RegisterVM
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Số điện thoại phải gồm đúng 8 chữ số.")]
    public string? Phone { get; set; }
}
