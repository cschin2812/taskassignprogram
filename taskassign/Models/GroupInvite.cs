using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public enum GroupInviteStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4
}

public class GroupInvite
{
    public int Id { get; set; }

    public int GroupId { get; set; }
    public Group? Group { get; set; }

    public int InvitedUserId { get; set; }
    public AppUser? InvitedUser { get; set; }

    public int InvitedByUserId { get; set; }
    public AppUser? InvitedByUser { get; set; }

    [Required, StringLength(120)]
    public string InviteEmail { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Token { get; set; } = string.Empty;

    public GroupInviteStatus Status { get; set; } = GroupInviteStatus.Pending;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
