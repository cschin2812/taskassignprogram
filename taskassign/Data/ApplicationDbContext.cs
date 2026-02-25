using Microsoft.EntityFrameworkCore;
using taskassign.Models;

namespace taskassign.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();
    public DbSet<UserGroupTableRow> UserGroupTableRows => Set<UserGroupTableRow>();
    public DbSet<PendingInviteRow> PendingInviteRows => Set<PendingInviteRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>().HasQueryFilter(t => !t.IsDel);
        modelBuilder.Entity<Group>().HasQueryFilter(g => !g.IsDel);
        modelBuilder.Entity<AppUser>().HasQueryFilter(u => !u.IsDel);



        modelBuilder.Entity<TaskItem>()
            .HasOne<Group>()
            .WithMany()
            .HasForeignKey(t => t.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Group>()
            .HasOne(g => g.Lead)
            .WithMany()
            .HasForeignKey(g => g.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasKey(gm => new { gm.GroupId, gm.MemberId });

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Member)
            .WithMany()
            .HasForeignKey(gm => gm.MemberId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.IsDel)
            .HasDefaultValue(false);

        modelBuilder.Entity<GroupInvite>()
            .HasIndex(i => i.Token)
            .IsUnique();

        modelBuilder.Entity<GroupInvite>()
            .HasOne(i => i.Group)
            .WithMany()
            .HasForeignKey(i => i.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupInvite>()
            .HasOne(i => i.InvitedUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupInvite>()
            .HasOne(i => i.InvitedByUser)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserGroupTableRow>().HasNoKey().ToView(null);
        modelBuilder.Entity<PendingInviteRow>().HasNoKey().ToView(null);
    }
}
