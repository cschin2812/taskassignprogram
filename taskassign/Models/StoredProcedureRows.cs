namespace taskassign.Models;

public class UserGroupTableRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LeadUsername { get; set; } = string.Empty;
    public string MembersCsv { get; set; } = string.Empty;
    public bool IsCurrentUserLead { get; set; }
}

public class PendingInviteRow
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string InviteEmail { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
