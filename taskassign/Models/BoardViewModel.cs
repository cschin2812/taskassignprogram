namespace taskassign.Models;

public class BoardViewModel
{
    public List<TaskItem> Tasks { get; set; } = new();
    public int ActiveGroupId { get; set; }
    public string ActiveGroupName { get; set; } = string.Empty;
}
