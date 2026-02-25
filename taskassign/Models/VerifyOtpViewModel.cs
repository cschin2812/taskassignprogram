using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public class VerifyOtpViewModel
{
    [Required]
    public int UserId { get; set; }

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    [Display(Name = "OTP")]
    public string Otp { get; set; } = string.Empty;
}
