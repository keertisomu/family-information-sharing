using FamilyCalendar.Api.Models;
using FamilyCalendar.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FamilyCalendar.Api.Data;

/// <summary>
/// Entity Framework Core database context for the Family Calendar application.
/// Implements multi-tenant data isolation through global query filters.
/// </summary>
public class ApplicationDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantMember> TenantMembers { get; set; } = null!;
    public DbSet<Invitation> Invitations { get; set; } = null!;
    public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureTenant(modelBuilder);
        ConfigureTenantMember(modelBuilder);
        ConfigureInvitation(modelBuilder);
        ConfigureCalendarEvent(modelBuilder);
        ConfigureGlobalQueryFilters(modelBuilder);
    }

    private void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.UserId);
            
            entity.Property(u => u.UserId)
                .HasColumnName("user_id")
                .ValueGeneratedOnAdd();
            
            entity.Property(u => u.GoogleId)
                .HasColumnName("google_id")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(u => u.Email)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(u => u.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(u => u.IsGlobalAdmin)
                .HasColumnName("is_global_admin")
                .IsRequired()
                .HasDefaultValue(false);
            
            entity.Property(u => u.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(u => u.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Indexes
            entity.HasIndex(u => u.GoogleId)
                .IsUnique()
                .HasDatabaseName("ix_users_google_id");
            
            entity.HasIndex(u => u.Email)
                .HasDatabaseName("ix_users_email");
        });
    }

    private void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(t => t.TenantId);
            
            entity.Property(t => t.TenantId)
                .HasColumnName("tenant_id")
                .ValueGeneratedOnAdd();
            
            entity.Property(t => t.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(t => t.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(t => t.OwnerId)
                .HasColumnName("owner_id")
                .IsRequired();
            
            entity.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasOne(t => t.Owner)
                .WithMany(u => u.OwnedTenants)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(t => t.OwnerId)
                .HasDatabaseName("ix_tenants_owner_id");
        });
    }

    private void ConfigureTenantMember(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantMember>(entity =>
        {
            entity.ToTable("tenant_members");
            entity.HasKey(tm => tm.TenantMemberId);
            
            entity.Property(tm => tm.TenantMemberId)
                .HasColumnName("tenant_member_id")
                .ValueGeneratedOnAdd();
            
            entity.Property(tm => tm.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();
            
            entity.Property(tm => tm.UserId)
                .HasColumnName("user_id")
                .IsRequired();
            
            entity.Property(tm => tm.Role)
                .HasColumnName("role")
                .HasMaxLength(50)
                .HasConversion<string>()
                .IsRequired();
            
            entity.Property(tm => tm.JoinedAt)
                .HasColumnName("joined_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasOne(tm => tm.Tenant)
                .WithMany(t => t.Members)
                .HasForeignKey(tm => tm.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(tm => tm.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(tm => tm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(tm => new { tm.TenantId, tm.UserId })
                .IsUnique()
                .HasDatabaseName("ix_tenant_members_tenant_user");
            
            entity.HasIndex(tm => tm.TenantId)
                .HasDatabaseName("ix_tenant_members_tenant_id");
            
            entity.HasIndex(tm => tm.UserId)
                .HasDatabaseName("ix_tenant_members_user_id");
        });
    }

    private void ConfigureInvitation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(i => i.InvitationId);
            
            entity.Property(i => i.InvitationId)
                .HasColumnName("invitation_id")
                .ValueGeneratedOnAdd();
            
            entity.Property(i => i.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();
            
            entity.Property(i => i.InvitedEmail)
                .HasColumnName("invited_email")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(i => i.InviterId)
                .HasColumnName("inviter_id")
                .IsRequired();
            
            entity.Property(i => i.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .HasConversion<string>()
                .IsRequired();
            
            entity.Property(i => i.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(i => i.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();
            
            entity.Property(i => i.AcceptedAt)
                .HasColumnName("accepted_at");

            // Relationships
            entity.HasOne(i => i.Tenant)
                .WithMany(t => t.Invitations)
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(i => i.Inviter)
                .WithMany()
                .HasForeignKey(i => i.InviterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(i => new { i.TenantId, i.InvitedEmail, i.Status })
                .HasDatabaseName("ix_invitations_tenant_email_status");
            
            entity.HasIndex(i => i.InvitedEmail)
                .HasDatabaseName("ix_invitations_invited_email");
            
            entity.HasIndex(i => i.ExpiresAt)
                .HasDatabaseName("ix_invitations_expires_at");
        });
    }

    private void ConfigureCalendarEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.ToTable("calendar_events");
            entity.HasKey(e => e.EventId);
            
            entity.Property(e => e.EventId)
                .HasColumnName("event_id")
                .ValueGeneratedOnAdd();
            
            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();
            
            entity.Property(e => e.CreatorId)
                .HasColumnName("creator_id")
                .IsRequired();
            
            entity.Property(e => e.AssignedTo)
                .HasColumnName("assigned_to");
            
            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.StartTime)
                .HasColumnName("start_time")
                .IsRequired();
            
            entity.Property(e => e.EndTime)
                .HasColumnName("end_time")
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Events)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Creator)
                .WithMany(u => u.CreatedEvents)
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Assignee)
                .WithMany(u => u.AssignedEvents)
                .HasForeignKey(e => e.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => new { e.TenantId, e.StartTime })
                .HasDatabaseName("ix_calendar_events_tenant_start_time");
            
            entity.HasIndex(e => e.CreatorId)
                .HasDatabaseName("ix_calendar_events_creator_id");
            
            entity.HasIndex(e => e.AssignedTo)
                .HasDatabaseName("ix_calendar_events_assigned_to");

            // Table configuration with check constraint
            entity.ToTable(t => t.HasCheckConstraint("ck_calendar_events_end_after_start", "end_time > start_time"));
        });
    }

    private void ConfigureGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Tenant-scoped entities auto-filter by TenantId
        modelBuilder.Entity<TenantMember>()
            .HasQueryFilter(tm => tm.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

        modelBuilder.Entity<Invitation>()
            .HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

        modelBuilder.Entity<CalendarEvent>()
            .HasQueryFilter(e => e.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

        // Tenant entity itself filtered by owner or membership
        modelBuilder.Entity<Tenant>()
            .HasQueryFilter(t => t.OwnerId == _tenantProvider.GetUserId() || 
                                 _tenantProvider.IsGlobalAdmin() ||
                                 t.Members.Any(m => m.UserId == _tenantProvider.GetUserId()));
    }
}
