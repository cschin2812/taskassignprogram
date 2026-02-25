using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using taskassign.Data;
using taskassign.Models;
using taskassign.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace taskassign.Controllers;

[Authorize]
public class GroupController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<GroupController> _logger;

    public GroupController(ApplicationDbContext db, IEmailSender emailSender, ILogger<GroupController> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public IActionResult Index(int groupsPage = 1, int pendingPage = 1)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Forbid();
        }

        var groups = _db.UserGroupTableRows
            .FromSqlInterpolated($"EXEC dbo.GetUserGroupsTableData @UserId={currentUserId}")
            .AsNoTracking()
            .AsEnumerable()
            .Select(g => new GroupListItemViewModel
            {
                Id = g.Id,
                Name = g.Name,
                LeadUsername = g.LeadUsername,
                Members = string.IsNullOrWhiteSpace(g.MembersCsv)
                    ? new List<string>()
                    : g.MembersCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                IsCurrentUserLead = g.IsCurrentUserLead
            })
            .ToList();

        var allPendingInvites = LoadPendingInvites(currentUserId);

        var groupsTotalPages = Math.Max(1, (int)Math.Ceiling(groups.Count / (double)PageSize));
        var groupsPageNumber = Math.Min(Math.Max(groupsPage, 1), groupsTotalPages);
        var pagedGroups = groups.Skip((groupsPageNumber - 1) * PageSize).Take(PageSize).ToList();

        var pendingTotalPages = Math.Max(1, (int)Math.Ceiling(allPendingInvites.Count / (double)PageSize));
        var pendingPageNumber = Math.Min(Math.Max(pendingPage, 1), pendingTotalPages);
        var pendingInvites = allPendingInvites.Skip((pendingPageNumber - 1) * PageSize).Take(PageSize).ToList();

        var model = new GroupManagementViewModel
        {
            Groups = pagedGroups,
            PendingInvites = pendingInvites,
            InviteGroupId = groups.FirstOrDefault(g => g.IsCurrentUserLead)?.Id ?? 0,
            GroupsPageNumber = groupsPageNumber,
            GroupsTotalPages = groupsTotalPages,
            PendingPageNumber = pendingPageNumber,
            PendingTotalPages = pendingTotalPages
        };

        return View(model);
    }



    [HttpGet]
    public IActionResult Detail(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Forbid();
        }

        var group = _db.Groups
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                LeadId = g.LeadId,
                LeadUsername = g.Lead != null ? g.Lead.Username : string.Empty
            })
            .FirstOrDefault();

        if (group == null)
        {
            TempData["GroupError"] = "Group not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!GroupAccessService.CanAccessGroup(_db, currentUserId, id))
        {
            return Forbid();
        }

        var members = _db.GroupMembers
            .Where(m => m.GroupId == id)
            .Select(m => new GroupMemberItemViewModel
            {
                UserId = m.MemberId,
                Username = m.Member != null ? m.Member.Username : string.Empty,
                DisplayName = m.Member != null ? m.Member.DisplayName : string.Empty,
                Email = m.Member != null ? m.Member.Email : string.Empty
            })
            .OrderBy(m => m.Username)
            .ToList();

        var pendingInvites = _db.GroupInvites
            .Where(i => i.GroupId == id && i.Status == GroupInviteStatus.Pending && i.ExpiresAt > DateTime.UtcNow)
            .Select(i => new GroupInviteItemViewModel
            {
                InviteId = i.Id,
                InvitedUserId = i.InvitedUserId,
                InviteEmail = i.InviteEmail,
                InvitedUsername = i.InvitedUser != null ? i.InvitedUser.Username : string.Empty,
                ExpiresAt = i.ExpiresAt
            })
            .OrderBy(i => i.ExpiresAt)
            .ToList();

        var model = new GroupDetailViewModel
        {
            GroupId = group.Id,
            GroupName = group.Name,
            LeadUsername = group.LeadUsername,
            IsCurrentUserLead = group.LeadId == currentUserId,
            Members = members,
            PendingInvites = pendingInvites
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveMember(int groupId, int memberId)
    {
        var currentUserId = GetCurrentUserId();
        if (!GroupAccessService.IsGroupLead(_db, currentUserId, groupId))
        {
            return Forbid();
        }

        var group = _db.Groups
            .Where(g => g.Id == groupId)
            .Select(g => new { g.Id, g.LeadId })
            .FirstOrDefault();

        if (group == null)
        {
            TempData["GroupError"] = "Group not found.";
            return RedirectToAction(nameof(Index));
        }

        if (group.LeadId == memberId)
        {
            TempData["GroupError"] = "Leader cannot be removed from group.";
            return RedirectToAction(nameof(Detail), new { id = groupId });
        }

        var member = _db.GroupMembers.FirstOrDefault(m => m.GroupId == groupId && m.MemberId == memberId);
        if (member == null)
        {
            TempData["GroupError"] = "Member not found in this group.";
            return RedirectToAction(nameof(Detail), new { id = groupId });
        }

        _db.GroupMembers.Remove(member);

        var now = DateTime.UtcNow;
        var invites = _db.GroupInvites
            .Where(i => i.GroupId == groupId && i.InvitedUserId == memberId && i.Status == GroupInviteStatus.Pending)
            .ToList();
        foreach (var invite in invites)
        {
            invite.Status = GroupInviteStatus.Expired;
            invite.RespondedAt = now;
        }

        _db.SaveChanges();

        TempData["GroupSuccess"] = "Member removed successfully.";
        return RedirectToAction(nameof(Detail), new { id = groupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelInvite(int groupId, int inviteId)
    {
        var currentUserId = GetCurrentUserId();
        if (!GroupAccessService.IsGroupLead(_db, currentUserId, groupId))
        {
            return Forbid();
        }

        var invite = _db.GroupInvites
            .FirstOrDefault(i => i.Id == inviteId && i.GroupId == groupId && i.Status == GroupInviteStatus.Pending);

        if (invite == null)
        {
            TempData["GroupError"] = "Pending invite not found.";
            return RedirectToAction(nameof(Detail), new { id = groupId });
        }

        invite.Status = GroupInviteStatus.Expired;
        invite.RespondedAt = DateTime.UtcNow;
        _db.SaveChanges();

        TempData["GroupSuccess"] = "Invite canceled successfully.";
        return RedirectToAction(nameof(Detail), new { id = groupId });
    }

    private List<PendingInviteViewModel> LoadPendingInvites(int userId)
    {
        try
        {
            return _db.PendingInviteRows
                .FromSqlInterpolated($"EXEC dbo.GetPendingInvitesByUserId @UserId={userId}")
                .AsNoTracking()
                .AsEnumerable()
                .Select(i => new PendingInviteViewModel
                {
                    Id = i.Id,
                    GroupName = i.GroupName,
                    InviteEmail = i.InviteEmail,
                    Token = i.Token,
                    ExpiresAt = i.ExpiresAt
                })
                .ToList();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Token", StringComparison.OrdinalIgnoreCase))
        {
            return _db.GroupInvites
                .Where(i => i.InvitedUserId == userId && i.Status == GroupInviteStatus.Pending && i.Group != null && !i.Group.IsDel)
                .Select(i => new PendingInviteViewModel
                {
                    Id = i.Id,
                    GroupName = i.Group!.Name,
                    InviteEmail = i.InviteEmail,
                    Token = i.Token,
                    ExpiresAt = i.ExpiresAt
                })
                .OrderBy(i => i.ExpiresAt)
                .ToList();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(GroupManagementViewModel model)
    {
        var currentUserId = GetCurrentUserId();
        var lead = _db.Users
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.Id })
            .FirstOrDefault();
        if (lead == null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["GroupError"] = "Group name is required.";
            return RedirectToAction(nameof(Index));
        }

        var group = new Group
        {
            Name = model.Name.Trim(),
            LeadId = lead.Id,
            IsDel = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Groups.Add(group);
        _db.SaveChanges();

        if (!string.IsNullOrWhiteSpace(model.CreateInviteEmail))
        {
            var createInviteResult = InviteMultipleUsersAsync(group.Id, model.CreateInviteEmail, lead.Id);
            if (createInviteResult.SuccessCount == 0)
            {
                TempData["GroupError"] = createInviteResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            Response.Cookies.Append("ActiveGroupId", group.Id.ToString());
            TempData["GroupSuccess"] = BuildCreateSuccessMessage(createInviteResult.SuccessCount, createInviteResult.FailedCount);
            TempData["InviteSuccessModal"] = BuildInviteModalMessage(createInviteResult.SuccessEmails, createInviteResult.FailedEmails);
            return RedirectToAction(nameof(Index));
        }

        Response.Cookies.Append("ActiveGroupId", group.Id.ToString());
        TempData["GroupSuccess"] = "Group created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Invite(GroupManagementViewModel model)
    {
        var currentUserId = GetCurrentUserId();
        var inviter = _db.Users
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.Id })
            .FirstOrDefault();
        var group = _db.Groups.FirstOrDefault(g => g.Id == model.InviteGroupId);

        if (inviter == null || group == null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!GroupAccessService.IsGroupLead(_db, currentUserId, group.Id))
        {
            return Forbid();
        }

        var inviteResult = InviteMultipleUsersAsync(group.Id, model.InviteEmail, inviter.Id);

        if (inviteResult.SuccessCount > 0)
        {
            TempData["GroupSuccess"] = BuildInviteSuccessMessage(inviteResult.SuccessCount, inviteResult.FailedCount);
            TempData["InviteSuccessModal"] = BuildInviteModalMessage(inviteResult.SuccessEmails, inviteResult.FailedEmails);
        }
        else
        {
            TempData["GroupError"] = inviteResult.ErrorMessage;
        }

        return RedirectToAction(nameof(Index));
    }


    [HttpGet]
    public IActionResult CheckInviteEmail(string email)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Json(new { found = false, message = "Please enter an email." });
        }

        var emailValidator = new EmailAddressAttribute();
        if (!emailValidator.IsValid(normalizedEmail))
        {
            return Json(new { found = false, message = "Invalid email format." });
        }

        var exists = _db.Users.Any(u => u.Email == normalizedEmail);
        if (!exists)
        {
            return Json(new { found = false, message = "User not found." });
        }

        return Json(new { found = true, email = normalizedEmail });
    }

    [HttpGet]
    public IActionResult AcceptInvite(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(Index));
        }

        var invite = _db.GroupInvites.FirstOrDefault(i => i.Token == token);
        if (invite == null)
        {
            TempData["GroupError"] = "Invite link is invalid.";
            return RedirectToAction(nameof(Index));
        }

        if (invite.Status != GroupInviteStatus.Pending || invite.ExpiresAt < DateTime.UtcNow)
        {
            TempData["GroupError"] = "Invite link is expired or already used.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Token = token;
        ViewBag.GroupName = _db.Groups.Where(g => g.Id == invite.GroupId).Select(g => g.Name).FirstOrDefault() ?? "Unknown";
        ViewBag.InviteEmail = invite.InviteEmail;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AcceptInviteConfirmed(string token, bool accept)
    {
        var currentUserId = GetCurrentUserId();
        var currentUser = _db.Users
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.Id, Email = u.Email ?? string.Empty })
            .FirstOrDefault();

        var inviteSnapshot = _db.GroupInvites
            .Where(i => i.Token == token)
            .Select(i => new
            {
                i.Id,
                i.GroupId,
                InviteEmail = i.InviteEmail ?? string.Empty,
                Status = (GroupInviteStatus?)i.Status ?? GroupInviteStatus.Expired,
                ExpiresAt = (DateTime?)i.ExpiresAt ?? DateTime.MinValue
            })
            .FirstOrDefault();

        if (currentUser == null || inviteSnapshot == null)
        {
            TempData["GroupError"] = "Invite cannot be processed.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(currentUser.Email, inviteSnapshot.InviteEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["GroupError"] = "Please login with invited email account to accept this invite.";
            return RedirectToAction(nameof(Index));
        }

        var inviteToUpdate = new GroupInvite { Id = inviteSnapshot.Id };
        _db.GroupInvites.Attach(inviteToUpdate);

        if (inviteSnapshot.Status != GroupInviteStatus.Pending || inviteSnapshot.ExpiresAt < DateTime.UtcNow)
        {
            inviteToUpdate.Status = GroupInviteStatus.Expired;
            inviteToUpdate.RespondedAt = DateTime.UtcNow;
            _db.SaveChanges();
            TempData["GroupError"] = "Invite link is expired or already used.";
            return RedirectToAction(nameof(Index));
        }

        inviteToUpdate.Status = accept ? GroupInviteStatus.Accepted : GroupInviteStatus.Declined;
        inviteToUpdate.RespondedAt = DateTime.UtcNow;

        if (accept)
        {
            var memberExists = _db.GroupMembers.Any(gm => gm.GroupId == inviteSnapshot.GroupId && gm.MemberId == currentUser.Id);
            var isLead = _db.Groups.Any(g => g.Id == inviteSnapshot.GroupId && g.LeadId == currentUser.Id);
            if (!memberExists && !isLead)
            {
                _db.GroupMembers.Add(new GroupMember { GroupId = inviteSnapshot.GroupId, MemberId = currentUser.Id });
            }

            TempData["GroupSuccess"] = "Invite accepted. You have joined the group.";
        }
        else
        {
            TempData["GroupSuccess"] = "Invite declined.";
        }

        _db.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        if (id <= 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var group = _db.Groups.FirstOrDefault(g => g.Id == id);
        if (group == null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!GroupAccessService.IsGroupLead(_db, GetCurrentUserId(), group.Id))
        {
            return Forbid();
        }

        group.IsDel = true;
        group.UpdatedAt = DateTime.Now;

        _ = int.TryParse(Request.Cookies["ActiveGroupId"], out var activeGroupId);
        if (activeGroupId == group.Id)
        {
            Response.Cookies.Delete("ActiveGroupId");
        }

        _db.SaveChanges();

        return RedirectToAction(nameof(Index));
    }

    private InviteBatchResult InviteMultipleUsersAsync(int groupId, string inviteEmails, int inviterId)
    {
        var result = new InviteBatchResult();
        var emails = ParseInviteEmails(inviteEmails).ToList();

        if (!emails.Any())
        {
            result.ErrorMessage = "Please enter at least one valid email.";
            return result;
        }

        foreach (var email in emails)
        {
            var inviteResult = CreateInviteAndSendEmailAsync(groupId, email, inviterId);
            if (inviteResult.Success)
            {
                result.SuccessEmails.Add(email);
            }
            else
            {
                result.FailedEmails.Add($"{email}: {inviteResult.ErrorMessage}");
            }
        }

        if (result.SuccessCount == 0)
        {
            result.ErrorMessage = result.FailedEmails.Any()
                ? string.Join(" ", result.FailedEmails)
                : "Invite failed.";
        }

        return result;
    }

    private InviteResult CreateInviteAndSendEmailAsync(int groupId, string inviteEmail, int inviterId)
    {
        var normalizedEmail = inviteEmail.Trim().ToLowerInvariant();
        var invitedUser = _db.Users
            .Where(u => u.Email == normalizedEmail)
            .Select(u => new { u.Id, Email = u.Email ?? string.Empty })
            .FirstOrDefault();
        if (invitedUser == null)
        {
            return InviteResult.Fail("User not found");
        }

        var group = _db.Groups.First(g => g.Id == groupId);

        if (group.LeadId == invitedUser.Id || _db.GroupMembers.Any(gm => gm.GroupId == groupId && gm.MemberId == invitedUser.Id))
        {
            return InviteResult.Fail("User already belongs to this group");
        }

        var existingPendingInvites = _db.GroupInvites
            .Where(i => i.GroupId == groupId
                && i.InvitedUserId == invitedUser.Id
                && i.Status == GroupInviteStatus.Pending
                && i.ExpiresAt > DateTime.UtcNow)
            .ToList();

        if (existingPendingInvites.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var pendingInvite in existingPendingInvites)
            {
                pendingInvite.Status = GroupInviteStatus.Expired;
                pendingInvite.RespondedAt = now;
            }
        }

        var token = Guid.NewGuid().ToString("N");
        var invite = new GroupInvite
        {
            GroupId = groupId,
            InvitedUserId = invitedUser.Id,
            InvitedByUserId = inviterId,
            InviteEmail = invitedUser.Email,
            Token = token,
            Status = GroupInviteStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.GroupInvites.Add(invite);
        _db.SaveChanges();

        var acceptUrl = Url.Action(nameof(AcceptInvite), "Group", new { token }, Request.Scheme) ?? $"/Group/AcceptInvite?token={token}";

        _ = Task.Run(async () =>
        {
            try
            {
                await _emailSender.SendAsync(
                    invitedUser.Email,
                    $"Group invite: {group.Name}",
                    $"You are invited to join '{group.Name}'. Please open this link to accept or decline: {acceptUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send group invite email to {InviteEmail} for group {GroupId}.", invitedUser.Email, groupId);
            }
        });

        return InviteResult.Ok();
    }

    private static IEnumerable<string> ParseInviteEmails(string? rawEmails)
    {
        if (string.IsNullOrWhiteSpace(rawEmails))
        {
            return Enumerable.Empty<string>();
        }

        return rawEmails
            .Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(email => email.Trim().ToLowerInvariant())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct();
    }

    private static string BuildCreateSuccessMessage(int successCount, int failedCount)
    {
        if (failedCount == 0)
        {
            return $"Group created successfully and {successCount} invite(s) sent.";
        }

        return $"Group created successfully. {successCount} invite(s) sent, {failedCount} failed.";
    }

    private static string BuildInviteSuccessMessage(int successCount, int failedCount)
    {
        if (failedCount == 0)
        {
            return $"{successCount} invite(s) sent successfully.";
        }

        return $"{successCount} invite(s) sent, {failedCount} failed.";
    }

    private static string BuildInviteModalMessage(List<string> successEmails, List<string> failedEmails)
    {
        var builder = new StringBuilder();
        if (successEmails.Any())
        {
            builder.Append($"Invited successfully: {string.Join(", ", successEmails)}.");
        }

        if (failedEmails.Any())
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append($"Failed: {string.Join("; ", failedEmails)}.");
        }

        return builder.ToString();
    }

    private sealed class InviteResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        public static InviteResult Ok() => new() { Success = true };
        public static InviteResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
    }

    private sealed class InviteBatchResult
    {
        public List<string> SuccessEmails { get; } = new();
        public List<string> FailedEmails { get; } = new();
        public int SuccessCount => SuccessEmails.Count;
        public int FailedCount => FailedEmails.Count;
        public string ErrorMessage { get; set; } = string.Empty;
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
