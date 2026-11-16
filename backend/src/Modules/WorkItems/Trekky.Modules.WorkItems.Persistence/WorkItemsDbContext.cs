using Microsoft.EntityFrameworkCore;
using Trekky.Modules.WorkItems.Domain;
using Trekky.Persistence.Outbox;

namespace Trekky.Modules.WorkItems.Persistence;

/// <summary>The work_items schema: <c>work_items</c>, <c>item_counters</c>, <c>comments</c> and the module's <c>outbox</c> (docs/tech/data-model.md).</summary>
internal sealed class WorkItemsDbContext(DbContextOptions<WorkItemsDbContext> options) : DbContext(options)
{
    public const string Schema = "work_items";

    public const string WorkItemsTable = "work_items";

    public const string ItemCountersTable = "item_counters";

    public const string CommentsTable = "comments";

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<ItemCounter> ItemCounters => Set<ItemCounter>();

    public DbSet<WorkItemComment> Comments => Set<WorkItemComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutbox();

        modelBuilder.Entity<WorkItem>(item =>
        {
            item.ToTable(WorkItemsTable, table =>
            {
                table.HasCheckConstraint("ck_work_items_type", $"type IN ({SqlList(WorkItemTypes.All)})");
                table.HasCheckConstraint("ck_work_items_status", $"status IN ({SqlList(WorkItemStatuses.All)})");
                table.HasCheckConstraint("ck_work_items_done_at", $"(status = '{WorkItemStatuses.Done}') = (done_at IS NOT NULL)");
                table.HasCheckConstraint("ck_work_items_title", $"char_length(title) BETWEEN 1 AND {WorkItem.TitleMaxLength}");
                table.HasCheckConstraint(
                    "ck_work_items_description", $"char_length(description_md) <= {WorkItem.DescriptionMaxLength}");
                table.HasCheckConstraint("ck_work_items_number", "number > 0");
                table.HasCheckConstraint("ck_work_items_rank", "rank <> ''");
                table.HasCheckConstraint("ck_work_items_custom", "jsonb_typeof(custom) = 'object'");
            });
            item.HasKey(w => w.Id);
            item.Property(w => w.Id).ValueGeneratedNever();
            item.Property(w => w.ProjectKey).HasMaxLength(10).IsRequired();
            item.Property(w => w.Type).HasMaxLength(16).IsRequired();
            item.Property(w => w.Title).HasMaxLength(WorkItem.TitleMaxLength).IsRequired();
            item.Property(w => w.Status).HasMaxLength(16).IsRequired();
            // Collation "C": ranks compare byte by byte, as BoardRank orders them.
            item.Property(w => w.Rank).UseCollation("C").IsRequired();
            item.Property(w => w.Custom).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
            item.Property(w => w.Version).IsRowVersion();
            item.Ignore(w => w.Key);

            // Parents are items of the same project; the service checks that and the type rules (US-06 AC4, AC5).
            item.HasOne<WorkItem>().WithMany().HasForeignKey(w => w.ParentId).OnDelete(DeleteBehavior.Restrict);

            item.HasIndex(w => new { w.ProjectId, w.Number }).HasDatabaseName("ux_work_items_project_number").IsUnique();
            // Lookup by key (TRK-42): project keys are unique per organization and never change.
            item.HasIndex(w => new { w.OrgId, w.ProjectKey, w.Number }).HasDatabaseName("ux_work_items_org_key").IsUnique();
            // The list: newest update first, then id, per project (keyset pagination, US-08).
            item.HasIndex(w => new { w.OrgId, w.ProjectId, w.UpdatedAt, w.Id })
                .HasDatabaseName("ix_work_items_org_project_updated")
                .IsDescending(false, false, true, true);
            // The board: each column in rank order (US-11 AC4); unique, so two cards never share a place.
            item.HasIndex(w => new { w.OrgId, w.ProjectId, w.Status, w.Rank })
                .HasDatabaseName("ux_work_items_org_project_status_rank")
                .IsUnique();
            item.HasIndex(w => w.ParentId).HasDatabaseName("ix_work_items_parent");
            // "Assigned to me" and member removal (v1.1) look items up by assignee.
            item.HasIndex(w => new { w.OrgId, w.AssigneeId }).HasDatabaseName("ix_work_items_org_assignee");
        });

        modelBuilder.Entity<WorkItemComment>(comment =>
        {
            comment.ToTable(CommentsTable, table => table.HasCheckConstraint(
                "ck_comments_body", $"char_length(body_md) BETWEEN 1 AND {WorkItemComment.BodyMaxLength} AND char_length(btrim(body_md)) > 0"));
            comment.HasKey(c => c.Id);
            comment.Property(c => c.Id).ValueGeneratedNever();
            comment.Property(c => c.BodyMd).HasMaxLength(WorkItemComment.BodyMaxLength).IsRequired();
            comment.Property(c => c.Version).IsRowVersion();
            // Deleted comments are gone for every read (US-12 AC2); the row stays for the history's sake.
            comment.HasQueryFilter(c => c.DeletedAt == null);
            comment.HasOne<WorkItem>().WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Restrict);
            // An item's comments, oldest first (keyset pagination, US-12 AC1).
            comment.HasIndex(c => new { c.WorkItemId, c.CreatedAt, c.Id }).HasDatabaseName("ix_comments_item_created");
            comment.HasIndex(c => c.OrgId).HasDatabaseName("ix_comments_org");
        });

        modelBuilder.Entity<ItemCounter>(counter =>
        {
            counter.ToTable(ItemCountersTable, table => table.HasCheckConstraint("ck_item_counters_next", "next_number > 0"));
            counter.HasKey(c => c.ProjectId);
            counter.Property(c => c.ProjectId).ValueGeneratedNever();
            counter.HasIndex(c => c.OrgId);
        });
    }

    private static string SqlList(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
