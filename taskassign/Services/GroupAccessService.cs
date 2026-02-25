using Microsoft.EntityFrameworkCore;
using taskassign.Data;
using taskassign.Models;

namespace taskassign.Services;

public static class GroupAccessService
{
    public static List<GroupInfo> GetGroupsForUser(ApplicationDbContext db, int userId)
    {
        if (userId <= 0)
        {
            return new List<GroupInfo>();
        }

        return db.Groups
            .Where(g => g.LeadId == userId || g.Members.Any(m => m.MemberId == userId))
            .Select(g => new GroupInfo { Id = g.Id, Name = g.Name })
            .ToList();
    }

    public static bool CanAccessGroup(ApplicationDbContext db, int userId, int groupId)
    {
        if (userId <= 0 || groupId <= 0)
        {
            return false;
        }

        return db.Groups.Any(g =>
            g.Id == groupId
            && (g.LeadId == userId || g.Members.Any(m => m.MemberId == userId)));
    }

    public static bool IsGroupLead(ApplicationDbContext db, int userId, int groupId)
    {
        if (userId <= 0 || groupId <= 0)
        {
            return false;
        }

        return db.Groups.Any(g => g.Id == groupId && g.LeadId == userId);
    }

    public static string GetGroupName(ApplicationDbContext db, int groupId)
    {
        if (groupId <= 0)
        {
            return "No Group";
        }

        return db.Groups.Where(g => g.Id == groupId).Select(g => g.Name).FirstOrDefault() ?? "No Group";
    }
}
