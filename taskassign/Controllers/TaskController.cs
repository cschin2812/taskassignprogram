using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using taskassign.Data;
using taskassign.Models;
using taskassign.Services;
using TaskPriorityEnum = taskassign.Models.TaskPriority;
using TaskStatusEnum = taskassign.Models.TaskStatus;

namespace taskassign.Controllers;

[Authorize]
public class TaskController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _db;

    public TaskController(ApplicationDbContext db)
    {
        _db = db;
    }

    public IActionResult Dashboard(int page = 1)
    {
        var currentUserId = GetCurrentUserId();
        var activeGroup = ResolveActiveGroup();
        PrepareGroupSwitcher(activeGroup.Id);

        var currentUser = User.Identity?.Name ?? string.Empty;
        var allTasks = _db.Tasks
            .FromSqlInterpolated($@"EXEC dbo.GetDashboardTasksByUserId
                @UserId={currentUserId},
                @SelectedGroupId={activeGroup.Id}")
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Priority)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(allTasks.Count / (double)PageSize));
        var pageNumber = Math.Min(Math.Max(page, 1), totalPages);
        var tasks = allTasks.Skip((pageNumber - 1) * PageSize).Take(PageSize).ToList();

        var totalTasks = allTasks.Count;
        var overdueTasks = allTasks.Count(t => t.DueDate.Date < DateTime.Today
            && t.Status != TaskStatusEnum.Completed
            && t.Status != TaskStatusEnum.Closed);
        var highPriorityTasks = allTasks.Count(t => t.Priority == TaskPriorityEnum.High);
        var mediumPriorityTasks = allTasks.Count(t => t.Priority == TaskPriorityEnum.Medium);
        var lowPriorityTasks = allTasks.Count(t => t.Priority == TaskPriorityEnum.Low);
        var myTasks = allTasks.Count(t => t.AssignedTo.Equals(currentUser, StringComparison.OrdinalIgnoreCase));

        ViewBag.CanManageTasks = activeGroup.Id > 0 && IsCurrentUserLead(activeGroup.Id);
        ViewBag.LeadGroupIds = GetLeadGroupIds(currentUserId);
        ViewBag.CurrentUsername = currentUser;

        return View(new DashboardViewModel
        {
            Tasks = tasks,
            CurrentUser = currentUser,
            ScopeLabel = activeGroup.Name,
            PageNumber = pageNumber,
            TotalPages = totalPages,
            TotalTasks = totalTasks,
            OverdueTasks = overdueTasks,
            HighPriorityTasks = highPriorityTasks,
            MediumPriorityTasks = mediumPriorityTasks,
            LowPriorityTasks = lowPriorityTasks,
            MyTasks = myTasks
        });
    }

    public IActionResult Index(string? search, TaskStatusEnum? status, TaskPriorityEnum? priority, string? assignedTo, int? groupId, int page = 1)
    {
        var currentUserId = GetCurrentUserId();
        var activeGroup = ResolveActiveGroup();
        var groups = GroupAccessService.GetGroupsForUser(_db, currentUserId);
        var accessibleGroupIds = groups.Select(g => g.Id).ToList();

        var selectedGroupId = groupId ?? activeGroup.Id;
        if (selectedGroupId != 0 && !accessibleGroupIds.Contains(selectedGroupId))
        {
            selectedGroupId = activeGroup.Id;
        }

        var accessibleGroupIdsCsv = string.Join(",", accessibleGroupIds);
        var query = _db.Tasks
            .FromSqlInterpolated($@"EXEC dbo.GetFilteredTasks 
                @AccessibleGroupIdsCsv={accessibleGroupIdsCsv},
                @SelectedGroupId={selectedGroupId},
                @Search={search},
                @Status={(int?)status},
                @Priority={(int?)priority},
                @AssignedTo={assignedTo}")
            .IgnoreQueryFilters()
            .AsNoTracking();

        ViewBag.Users = _db.Users.Select(u => u.Username).ToList();
        ViewBag.TaskGroups = groups;
        ViewBag.TaskGroupNameMap = groups.ToDictionary(g => g.Id, g => g.Name);
        ViewBag.CanManageTasks = selectedGroupId > 0 && IsCurrentUserLead(selectedGroupId);
        ViewBag.LeadGroupIds = GetLeadGroupIds(currentUserId);
        ViewBag.CurrentUsername = User.Identity?.Name ?? string.Empty;
        PrepareGroupSwitcher(activeGroup.Id);

        var activeGroupName = selectedGroupId == 0
            ? "All Groups"
            : groups.FirstOrDefault(g => g.Id == selectedGroupId)?.Name ?? activeGroup.Name;

        var allTasks = query
            .AsEnumerable()
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Priority)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(allTasks.Count / (double)PageSize));
        var pageNumber = Math.Min(Math.Max(page, 1), totalPages);
        var tasks = allTasks.Skip((pageNumber - 1) * PageSize).Take(PageSize).ToList();

        return View(new TaskQueryViewModel
        {
            Tasks = tasks,
            Search = search,
            Status = status,
            Priority = priority,
            AssignedTo = assignedTo,
            ActiveGroupId = selectedGroupId,
            ActiveGroupName = activeGroupName,
            SelectedGroupId = selectedGroupId,
            PageNumber = pageNumber,
            TotalPages = totalPages
        });
    }

    public IActionResult Board()
    {
        var currentUserId = GetCurrentUserId();
        var activeGroup = ResolveActiveGroup();
        PrepareGroupSwitcher(activeGroup.Id);

        var groups = GroupAccessService.GetGroupsForUser(_db, currentUserId);
        var groupIds = groups.Select(g => g.Id).ToList();
        var tasks = activeGroup.Id == 0
            ? _db.Tasks.Where(t => groupIds.Contains(t.GroupId)).ToList()
            : _db.Tasks.Where(t => t.GroupId == activeGroup.Id).ToList();

        ViewBag.LeadGroupIds = GetLeadGroupIds(currentUserId);

        return View(new BoardViewModel
        {
            Tasks = tasks,
            ActiveGroupId = activeGroup.Id,
            ActiveGroupName = activeGroup.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateNote(int id, string? note)
    {
        var task = _db.Tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return Json(new { success = false, message = "Task not found" });
        }

        var currentUsername = User.Identity?.Name;
        if (!CanAccessTask(task) || !string.Equals(task.AssignedTo, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, message = "Only assignee can update note" });
        }

        task.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        task.UpdatedAt = DateTime.Now;
        _db.SaveChanges();

        return Json(new { success = true, note = task.Note ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(int id, TaskStatusEnum status)
    {
        var task = _db.Tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            return Json(new { success = false, message = "Task not found" });
        }

        if (!CanAccessTask(task) || !CanUpdateStatus(task))
        {
            return Json(new { success = false, message = "No permission" });
        }

        task.Status = status;
        task.UpdatedAt = DateTime.Now;
        _db.SaveChanges();

        return Json(new { success = true });
    }

    public IActionResult Create()
    {
        var activeGroup = ResolveActiveGroup();
        if (!IsCurrentUserLead(activeGroup.Id))
        {
            return Forbid();
        }

        PopulateUsers(activeGroup.Id);
        PrepareGroupSwitcher(activeGroup.Id);

        return View(new TaskItem
        {
            DueDate = DateTime.Today.AddDays(1),
            Status = TaskStatusEnum.New,
            Priority = TaskPriorityEnum.Medium,
            GroupId = activeGroup.Id,
            CreatedBy = User.Identity?.Name ?? "system"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(TaskItem task)
    {
        var activeGroup = ResolveActiveGroup();
        if (!IsCurrentUserLead(activeGroup.Id))
        {
            return Forbid();
        }

        task.GroupId = activeGroup.Id;
        task.CreatedBy = User.Identity?.Name ?? "system";

        if (!ModelState.IsValid)
        {
            PopulateUsers(activeGroup.Id);
            PrepareGroupSwitcher(activeGroup.Id);
            return View(task);
        }

        task.CreatedAt = DateTime.Now;
        task.UpdatedAt = DateTime.Now;
        _db.Tasks.Add(task);
        _db.SaveChanges();

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var task = _db.Tasks.Find(id);
        if (task == null)
        {
            return NotFound();
        }

        var canEditTaskContent = CanEdit(task);
        var canUpdateStatus = CanUpdateStatus(task);
        var canUpdateNote = CanUpdateNote(task);

        if (!CanAccessTask(task) || (!canEditTaskContent && !canUpdateStatus && !canUpdateNote))
        {
            return Forbid();
        }

        PopulateUsers(task.GroupId);
        PrepareGroupSwitcher(task.GroupId);
        ViewBag.CanEditTaskContent = canEditTaskContent;
        ViewBag.CanUpdateStatus = canUpdateStatus;
        ViewBag.CanUpdateNote = canUpdateNote || canEditTaskContent;

        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(TaskItem task)
    {
        var existing = _db.Tasks.AsNoTracking().FirstOrDefault(t => t.Id == task.Id);
        if (existing == null)
        {
            return NotFound();
        }

        var canEditTaskContent = CanEdit(existing);
        var canUpdateStatus = CanUpdateStatus(existing);
        var canUpdateNote = CanUpdateNote(existing);

        if (!CanAccessTask(existing) || (!canEditTaskContent && !canUpdateStatus && !canUpdateNote))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            task.GroupId = existing.GroupId;
            task.CreatedBy = existing.CreatedBy;
            PopulateUsers(existing.GroupId);
            PrepareGroupSwitcher(existing.GroupId);
            ViewBag.CanEditTaskContent = canEditTaskContent;
            ViewBag.CanUpdateStatus = canUpdateStatus;
            ViewBag.CanUpdateNote = canUpdateNote || canEditTaskContent;
            return View(task);
        }

        task.GroupId = existing.GroupId;
        task.CreatedBy = existing.CreatedBy;
        task.CreatedAt = existing.CreatedAt;

        if (!canEditTaskContent)
        {
            task.Title = existing.Title;
            task.Description = existing.Description;
            task.AssignedTo = existing.AssignedTo;
            task.Priority = existing.Priority;
            task.DueDate = existing.DueDate;
        }

        if (!canUpdateStatus)
        {
            task.Status = existing.Status;
        }

        if (!(canUpdateNote || canEditTaskContent))
        {
            task.Note = existing.Note;
        }

        task.UpdatedAt = DateTime.Now;

        _db.Tasks.Update(task);
        _db.SaveChanges();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var task = _db.Tasks.Find(id);
        if (task != null && CanAccessTask(task) && IsCurrentUserLead(task.GroupId))
        {
            task.IsDel = true;
            task.UpdatedAt = DateTime.Now;
            _db.SaveChanges();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SwitchGroup(int groupId, string? returnUrl)
    {
        var currentUserId = GetCurrentUserId();
        var canSwitchToAllGroups = groupId == 0 && GroupAccessService.GetGroupsForUser(_db, currentUserId).Any();
        if (canSwitchToAllGroups || GroupAccessService.CanAccessGroup(_db, currentUserId, groupId))
        {
            Response.Cookies.Append("ActiveGroupId", groupId.ToString());
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    private bool CanEdit(TaskItem task)
    {
        return IsCurrentUserLead(task.GroupId);
    }

    private bool CanUpdateStatus(TaskItem task)
    {
        if (IsCurrentUserLead(task.GroupId))
        {
            return true;
        }

        return string.Equals(task.AssignedTo, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanUpdateNote(TaskItem task)
    {
        return string.Equals(task.AssignedTo, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAccessTask(TaskItem task)
    {
        return GroupAccessService.CanAccessGroup(_db, GetCurrentUserId(), task.GroupId);
    }

    private bool IsCurrentUserLead(int groupId)
    {
        return GroupAccessService.IsGroupLead(_db, GetCurrentUserId(), groupId);
    }

    private HashSet<int> GetLeadGroupIds(int userId)
    {
        if (userId <= 0)
        {
            return new HashSet<int>();
        }

        return _db.Groups
            .Where(g => g.LeadId == userId)
            .Select(g => g.Id)
            .ToHashSet();
    }

    private GroupInfo ResolveActiveGroup()
    {
        var groups = GroupAccessService.GetGroupsForUser(_db, GetCurrentUserId());
        _ = int.TryParse(Request.Cookies["ActiveGroupId"], out var cookieGroupId);

        if (cookieGroupId == 0 && groups.Any())
        {
            return new GroupInfo { Id = 0, Name = "All Groups" };
        }

        var active = groups.FirstOrDefault(g => g.Id == cookieGroupId)
                     ?? groups.FirstOrDefault()
                     ?? new GroupInfo { Id = 0, Name = "No Group" };

        if (active.Id > 0)
        {
            Response.Cookies.Append("ActiveGroupId", active.Id.ToString());
        }

        return active;
    }

    private void PrepareGroupSwitcher(int activeGroupId)
    {
        var groups = GroupAccessService.GetGroupsForUser(_db, GetCurrentUserId());
        if (groups.Any())
        {
            groups.Insert(0, new GroupInfo { Id = 0, Name = "All Groups" });
        }

        ViewBag.AvailableGroups = groups;
        ViewBag.ActiveGroupId = activeGroupId;
        ViewBag.ActiveGroupName = activeGroupId == 0 ? "All Groups" : GroupAccessService.GetGroupName(_db, activeGroupId);
        ViewBag.CanManageTasks = activeGroupId > 0 && IsCurrentUserLead(activeGroupId);
    }

    private void PopulateUsers(int groupId)
    {
        var allowedUsernames = GroupAccessService.CanAccessGroup(_db, GetCurrentUserId(), groupId)
            ? _db.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Select(gm => gm.Member!.Username)
                .Union(_db.Groups.Where(g => g.Id == groupId).Select(g => g.Lead!.Username))
                .Distinct()
                .ToList()
            : new List<string>();

        if (!allowedUsernames.Any() && !string.IsNullOrWhiteSpace(User.Identity?.Name))
        {
            allowedUsernames.Add(User.Identity!.Name!);
        }

        ViewBag.Users = allowedUsernames;
    }

    private int GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claimValue, out var userId) && userId > 0)
        {
            return userId;
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return 0;
        }

        return _db.Users
            .Where(u => u.Username == username)
            .Select(u => u.Id)
            .FirstOrDefault();
    }
}
