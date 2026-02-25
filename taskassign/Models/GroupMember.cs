namespace taskassign.Models;

public class GroupMember
{
    public int GroupId { get; set; }
    public Group? Group { get; set; }

    public int MemberId { get; set; }
    public AppUser? Member { get; set; }
}
