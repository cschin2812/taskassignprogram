using System.ComponentModel.DataAnnotations;

namespace taskassign.Models;

public class GroupListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LeadUsername { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
    public bool IsCurrentUserLead { get; set; }
}

public class PendingInviteViewModel
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string InviteEmail { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class GroupMemberItemViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class GroupInviteItemViewModel
{
    public int InviteId { get; set; }
    public int InvitedUserId { get; set; }
    public string InviteEmail { get; set; } = string.Empty;
    public string InvitedUsername { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class GroupDetailViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string LeadUsername { get; set; } = string.Empty;
    public bool IsCurrentUserLead { get; set; }
    public List<GroupMemberItemViewModel> Members { get; set; } = new();
    public List<GroupInviteItemViewModel> PendingInvites { get; set; } = new();
}

public class GroupManagementViewModel
{
    public List<GroupListItemViewModel> Groups { get; set; } = new();
    public List<PendingInviteViewModel> PendingInvites { get; set; } = new();
    public int PendingPageNumber { get; set; } = 1;
    public int PendingTotalPages { get; set; } = 1;
    public int GroupsPageNumber { get; set; } = 1;
    public int GroupsTotalPages { get; set; } = 1;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public string? CreateInviteEmail { get; set; }

    [Required]
    public string InviteEmail { get; set; } = string.Empty;

    [Required]
    public int InviteGroupId { get; set; }
}
