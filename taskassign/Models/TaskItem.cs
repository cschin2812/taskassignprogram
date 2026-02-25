using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace taskassign.Models;

public enum TaskStatus
{
    New = 1,
    InProgress = 2,
    UAT = 3,
    Completed = 4,
    Closed = 5
}

public enum TaskPriority
{
    High = 1,
    Medium = 2,
    Low = 3
}

public class TaskItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required."), StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required."), StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "AssignedTo is required."), StringLength(80)]
    public string AssignedTo { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required]
    public int GroupId { get; set; }

    [Required(ErrorMessage = "DueDate is required.")]
    public DateTime DueDate { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public TaskStatus Status { get; set; }

    [Required(ErrorMessage = "Priority is required.")]
    public TaskPriority Priority { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Column("Is_Del")]
    public bool IsDel { get; set; }

    [NotMapped]
    public int RemainingDays => (DueDate.Date - DateTime.Today).Days;

    [NotMapped]
    public string DueDateStatusText => RemainingDays < 0
        ? "Overdue"
        : RemainingDays == 0
            ? "Due today"
            : $"{RemainingDays} day(s) left";

    [NotMapped]
    public string DueDateStatusClass => RemainingDays <= 0
        ? "text-danger"
        : RemainingDays <= 2
            ? "text-warning"
            : "text-success";

    [NotMapped]
    public string PriorityClass => Priority switch
    {
        TaskPriority.High => "text-danger",
        TaskPriority.Medium => "text-warning",
        _ => "text-primary"
    };
}
