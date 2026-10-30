using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Projects.Domain;
using Trekky.Persistence.Outbox;

namespace Trekky.Modules.Projects.Persistence;

/// <summary>The projects schema: <c>projects</c> and the module's <c>outbox</c> (docs/tech/data-model.md).</summary>
internal sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public const string Schema = "projects";

    public const string ProjectsTable = "projects";

    /// <summary>Unique key per organization; a violation means "key taken" (409).</summary>
    public const string KeyIndexName = "ux_projects_org_key";

    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutbox();

        modelBuilder.Entity<Project>(project =>
        {
            project.ToTable(ProjectsTable, table =>
            {
                table.HasCheckConstraint("ck_projects_key", $"key ~ '{Project.KeyPattern}'");
                table.HasCheckConstraint("ck_projects_name", $"char_length(name) BETWEEN 1 AND {Project.NameMaxLength}");
            });
            project.HasKey(p => p.Id);
            project.Property(p => p.Id).ValueGeneratedNever();
            project.Property(p => p.Key).HasMaxLength(Project.KeyMaxLength).IsRequired();
            project.Property(p => p.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
            project.Property(p => p.Version).IsRowVersion();

            project.HasIndex(p => new { p.OrgId, p.Key }).HasDatabaseName(KeyIndexName).IsUnique();
            // The list: ordered by name, then id, per organization (keyset pagination).
            project.HasIndex(p => new { p.OrgId, p.Name, p.Id }).HasDatabaseName("ix_projects_org_name");
        });
    }
}
