using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username or Email")]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
