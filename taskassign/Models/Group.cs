using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace taskassign.Models;

public class Group
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public int LeadId { get; set; }

    public AppUser? Lead { get; set; }

    public List<GroupMember> Members { get; set; } = new();

    [Column("Is_Del")]
    public bool IsDel { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
