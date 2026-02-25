namespace taskassign.Models;

public class DashboardViewModel
{
    public List<TaskItem> Tasks { get; set; } = new();
    public string CurrentUser { get; set; } = string.Empty;
    public string ScopeLabel { get; set; } = "All Groups";
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;

    public int TotalTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int HighPriorityTasks { get; set; }
    public int MediumPriorityTasks { get; set; }
    public int LowPriorityTasks { get; set; }
    public int MyTasks { get; set; }
}
