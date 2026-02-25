using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace taskassign.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Role { get; set; } = "Member";

    [Required, StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(120), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Column("Is_Del")]
    public bool IsDel { get; set; }

    [StringLength(32)]
    public string? OTP { get; set; }
}
