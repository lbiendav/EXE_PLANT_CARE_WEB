using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class LoginVM
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
