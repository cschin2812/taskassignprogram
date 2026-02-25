namespace taskassign.Models;

public class TaskQueryViewModel
{
    public List<TaskItem> Tasks { get; set; } = new();
    public string? Search { get; set; }
    public TaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public int ActiveGroupId { get; set; }
    public string ActiveGroupName { get; set; } = string.Empty;
    public int SelectedGroupId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}
