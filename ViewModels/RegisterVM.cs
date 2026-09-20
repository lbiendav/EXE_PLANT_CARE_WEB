using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class RegisterVM
{
    [Required]
    public string FullName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }

    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Số điện thoại phải gồm đúng 8 chữ số.")]
    public string? Phone { get; set; }
}
