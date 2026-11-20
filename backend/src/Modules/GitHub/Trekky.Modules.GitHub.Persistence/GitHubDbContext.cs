using Microsoft.EntityFrameworkCore;
using Trekky.Modules.GitHub.Domain;
using Trekky.Persistence.Outbox;

namespace Trekky.Modules.GitHub.Persistence;

/// <summary>
/// The github schema (TREK-74): connected app <c>installations</c>, each project's <c>project_repositories</c> link and
/// the module's <c>outbox</c>. No tokens or keys are stored here (ADR-0028).
/// </summary>
internal sealed class GitHubDbContext(DbContextOptions<GitHubDbContext> options) : DbContext(options)
{
    public const string Schema = "github";

    public const string InstallationsTable = "installations";

    public const string ProjectRepositoriesTable = "project_repositories";

    /// <summary>
    /// GitHub's installation id is unique across organizations: RLS hides the other organization's row, so a violation
    /// means "connected elsewhere" (409).
    /// </summary>
    public const string InstallationIdIndexName = "ux_installations_installation_id";

    /// <summary>One link per project; a violation means another request linked it first.</summary>
    public const string ProjectIndexName = "ux_project_repositories_project";

    public DbSet<GitHubInstallation> Installations => Set<GitHubInstallation>();

    public DbSet<ProjectRepository> ProjectRepositories => Set<ProjectRepository>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutbox();

        modelBuilder.Entity<GitHubInstallation>(installation =>
        {
            installation.ToTable(InstallationsTable, table =>
                table.HasCheckConstraint("ck_installations_installation_id", "installation_id > 0"));
            installation.HasKey(i => i.Id);
            installation.Property(i => i.Id).ValueGeneratedNever();
            installation.Property(i => i.AccountLogin).HasMaxLength(GitHubInstallation.AccountLoginMaxLength).IsRequired();
            installation.Property(i => i.AccountType).HasMaxLength(GitHubInstallation.AccountTypeMaxLength).IsRequired();

            installation.HasIndex(i => i.InstallationId).HasDatabaseName(InstallationIdIndexName).IsUnique();
            installation.HasIndex(i => i.OrgId).HasDatabaseName("ix_installations_org");
        });

        modelBuilder.Entity<ProjectRepository>(link =>
        {
            link.ToTable(ProjectRepositoriesTable, table =>
            {
                table.HasCheckConstraint("ck_project_repositories_repository_id", "repository_id > 0");
                table.HasCheckConstraint(
                    "ck_project_repositories_docs_path", $"char_length(docs_path) <= {DocsPath.MaxLength}");
                table.HasCheckConstraint(
                    "ck_project_repositories_branch", $"char_length(branch) BETWEEN 1 AND {BranchName.MaxLength}");
            });
            link.HasKey(l => l.Id);
            link.Property(l => l.Id).ValueGeneratedNever();
            link.Property(l => l.InstallationRowId).HasColumnName("installation_id");
            link.Property(l => l.RepositoryFullName).HasColumnName("repository").HasMaxLength(ProjectRepository.RepositoryMaxLength).IsRequired();
            link.Property(l => l.DocsPath).HasMaxLength(DocsPath.MaxLength).IsRequired();
            link.Property(l => l.Branch).HasMaxLength(BranchName.MaxLength).IsRequired();
            link.Property(l => l.Version).IsRowVersion();

            // Disconnecting an installation removes the links that used it (both rows are this module's own).
            link.HasOne<GitHubInstallation>()
                .WithMany()
                .HasForeignKey(l => l.InstallationRowId)
                .OnDelete(DeleteBehavior.Cascade);

            link.HasIndex(l => l.ProjectId).HasDatabaseName(ProjectIndexName).IsUnique();
            link.HasIndex(l => l.OrgId).HasDatabaseName("ix_project_repositories_org");
        });
    }
}
