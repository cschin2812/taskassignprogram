using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public class ForgotPasswordViewModel
{
    [Required, StringLength(120)]
    [Display(Name = "Username or Email")]
    public string Identifier { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required, StringLength(120)]
    [Display(Name = "Username or Email")]
    public string Identifier { get; set; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    [Display(Name = "OTP")]
    public string Otp { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
