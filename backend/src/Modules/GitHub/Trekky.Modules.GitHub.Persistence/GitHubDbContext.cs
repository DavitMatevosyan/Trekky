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

    public const string DocTreesTable = "doc_trees";

    public const string DocFilesTable = "doc_files";

    /// <summary>One cache per link; a violation means another request created it first.</summary>
    public const string DocTreeLinkIndexName = "ux_doc_trees_link";

    /// <summary>One row per path in a cache; a violation means another refresh wrote the file first.</summary>
    public const string DocFilePathIndexName = "ux_doc_files_tree_path";

    /// <summary>
    /// The Postgres text search configuration of the search queries (ADR-0033). It must match the one the
    /// <c>doc_files.search</c> column was generated with in migration DocsCache; changing it needs a new migration.
    /// <c>simple</c> lower-cases words without stemming, so it treats every language alike and matches code names as
    /// written.
    /// </summary>
    public const string SearchConfiguration = "simple";

    public DbSet<GitHubInstallation> Installations => Set<GitHubInstallation>();

    public DbSet<ProjectRepository> ProjectRepositories => Set<ProjectRepository>();

    public DbSet<DocTree> DocTrees => Set<DocTree>();

    public DbSet<DocFile> DocFiles => Set<DocFile>();

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

        modelBuilder.Entity<DocTree>(tree =>
        {
            tree.ToTable(DocTreesTable);
            tree.HasKey(t => t.Id);
            tree.Property(t => t.Id).ValueGeneratedNever();
            tree.Property(t => t.Source).HasMaxLength(64 + BranchName.MaxLength + DocsPath.MaxLength).IsRequired();
            tree.Property(t => t.CommitSha).HasMaxLength(64);
            tree.Property(t => t.CommitMessage).HasMaxLength(DocFile.TitleMaxLength);
            tree.Property(t => t.CommitAuthor).HasMaxLength(200);

            // Unlinking a project or disconnecting its installation drops the cache with the link.
            tree.HasOne<ProjectRepository>()
                .WithMany()
                .HasForeignKey(t => t.LinkId)
                .OnDelete(DeleteBehavior.Cascade);

            tree.HasIndex(t => t.LinkId).HasDatabaseName(DocTreeLinkIndexName).IsUnique();
            tree.HasIndex(t => t.OrgId).HasDatabaseName("ix_doc_trees_org");
        });

        modelBuilder.Entity<DocFile>(file =>
        {
            file.ToTable(DocFilesTable, table =>
                table.HasCheckConstraint("ck_doc_files_size", "size >= 0"));
            file.HasKey(f => f.Id);
            file.Property(f => f.Id).ValueGeneratedNever();
            file.Property(f => f.Path).HasMaxLength(DocFile.PathMaxLength).IsRequired();
            file.Property(f => f.BlobSha).HasMaxLength(64).IsRequired();
            file.Property(f => f.Title).HasMaxLength(DocFile.TitleMaxLength).IsRequired();
            file.Property(f => f.CommitSha).HasMaxLength(64);
            file.Property(f => f.CommitMessage).HasMaxLength(DocFile.TitleMaxLength);
            file.Property(f => f.CommitAuthor).HasMaxLength(200);

            file.HasOne<DocTree>()
                .WithMany()
                .HasForeignKey(f => f.TreeId)
                .OnDelete(DeleteBehavior.Cascade);

            file.HasIndex(f => new { f.TreeId, f.Path }).HasDatabaseName(DocFilePathIndexName).IsUnique();
            file.HasIndex(f => f.OrgId).HasDatabaseName("ix_doc_files_org");

            // The generated tsvector column "search" and its GIN index are added in the migration's SQL and are not
            // mapped: only the search query reads them (DocsSearch).
        });
    }
}
