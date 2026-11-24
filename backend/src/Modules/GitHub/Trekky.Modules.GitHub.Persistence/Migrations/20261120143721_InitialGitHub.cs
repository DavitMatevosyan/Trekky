using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.GitHub.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGitHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "github");

            migrationBuilder.CreateTable(
                name: "installations",
                schema: "github",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    account_login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    connected_by = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installations", x => x.id);
                    table.CheckConstraint("ck_installations_installation_id", "installation_id > 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "github",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    envelope = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_repositories",
                schema: "github",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<long>(type: "bigint", nullable: false),
                    repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    docs_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_repositories", x => x.id);
                    table.CheckConstraint("ck_project_repositories_branch", "char_length(branch) BETWEEN 1 AND 255");
                    table.CheckConstraint("ck_project_repositories_docs_path", "char_length(docs_path) <= 200");
                    table.CheckConstraint("ck_project_repositories_repository_id", "repository_id > 0");
                    table.ForeignKey(
                        name: "fk_project_repositories_installations_installation_id",
                        column: x => x.installation_id,
                        principalSchema: "github",
                        principalTable: "installations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_installations_org",
                schema: "github",
                table: "installations",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ux_installations_installation_id",
                schema: "github",
                table: "installations",
                column: "installation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_org_id",
                schema: "github",
                table: "outbox",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "github",
                table: "outbox",
                column: "id",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_repositories_installation_id",
                schema: "github",
                table: "project_repositories",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_repositories_org",
                schema: "github",
                table: "project_repositories",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ux_project_repositories_project",
                schema: "github",
                table: "project_repositories",
                column: "project_id",
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.InstallationsTable);
            migrationBuilder.EnableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.ProjectRepositoriesTable);
            migrationBuilder.EnableOutboxRowLevelSecurity(GitHubDbContext.Schema);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableOutboxRowLevelSecurity(GitHubDbContext.Schema);
            migrationBuilder.DisableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.ProjectRepositoriesTable);
            migrationBuilder.DisableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.InstallationsTable);

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "github");

            migrationBuilder.DropTable(
                name: "project_repositories",
                schema: "github");

            migrationBuilder.DropTable(
                name: "installations",
                schema: "github");
        }
    }
}
