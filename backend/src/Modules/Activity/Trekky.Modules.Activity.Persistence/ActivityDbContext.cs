using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Activity.Domain;

namespace Trekky.Modules.Activity.Persistence;

/// <summary>The activity schema: the append-only <c>activity_events</c> log (docs/tech/activity-events.md).</summary>
internal sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options)
{
    public const string Schema = "activity";

    public const string EventsTable = "activity_events";

    public DbSet<ActivityEvent> Events => Set<ActivityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ActivityEvent>(activity =>
        {
            activity.ToTable(EventsTable);
            activity.HasKey(e => e.EventId);
            activity.Property(e => e.EventId).ValueGeneratedNever();
            activity.Property(e => e.EventType).HasMaxLength(ActivityEvent.EventTypeMaxLength).IsRequired();
            activity.Property(e => e.ActorType).HasMaxLength(ActivityEvent.ActorTypeMaxLength).IsRequired();
            activity.Property(e => e.EntityType).HasMaxLength(ActivityEvent.EntityTypeMaxLength).IsRequired();
            activity.Property(e => e.EntityKey).HasMaxLength(ActivityEvent.EntityKeyMaxLength);
            activity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();

            // Item history and project feeds, newest first (UUIDv7 event ids sort by time).
            activity.HasIndex(e => new { e.OrgId, e.EntityId, e.EventId })
                .HasDatabaseName("ix_activity_events_entity")
                .IsDescending(false, false, true);
            activity.HasIndex(e => new { e.OrgId, e.ProjectId, e.EventId })
                .HasDatabaseName("ix_activity_events_project")
                .IsDescending(false, false, true);
        });
    }
}
