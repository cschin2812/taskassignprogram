using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public class RegisterViewModel
{
    [Required, StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(120), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(150), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
