using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.GitHub.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocsCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doc_trees",
                schema: "github",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(519)", maxLength: 519, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    commit_message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    commit_author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    folder_missing = table.Column<bool>(type: "boolean", nullable: false),
                    truncated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doc_trees", x => x.id);
                    table.ForeignKey(
                        name: "fk_doc_trees_project_repositories_link_id",
                        column: x => x.link_id,
                        principalSchema: "github",
                        principalTable: "project_repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doc_files",
                schema: "github",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tree_id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    blob_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    size = table.Column<int>(type: "integer", nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    commit_message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    commit_author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doc_files", x => x.id);
                    table.CheckConstraint("ck_doc_files_size", "size >= 0");
                    table.ForeignKey(
                        name: "fk_doc_files_doc_trees_tree_id",
                        column: x => x.tree_id,
                        principalSchema: "github",
                        principalTable: "doc_trees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doc_files_org",
                schema: "github",
                table: "doc_files",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ux_doc_files_tree_path",
                schema: "github",
                table: "doc_files",
                columns: new[] { "tree_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doc_trees_org",
                schema: "github",
                table: "doc_trees",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ux_doc_trees_link",
                schema: "github",
                table: "doc_trees",
                column: "link_id",
                unique: true);

            // Full-text search over the cached docs (ADR-0033): the title weighs more than the body. Generated by
            // Postgres, so every write keeps it current; not mapped in the model. Only the first 100,000 characters of
            // a doc are indexed: a tsvector may not exceed 1 MB, and a doc of many distinct words (pasted base64, for
            // example) would otherwise fail its insert, and with it every refresh of the folder. The configuration and
            // the 100,000-character limit are written out, not taken from GitHubDbContext.SearchConfiguration, so this
            // migration keeps describing the column it created if that constant changes later (that needs a new migration).
            migrationBuilder.Sql("""
                ALTER TABLE github.doc_files ADD COLUMN search tsvector GENERATED ALWAYS AS (
                    setweight(to_tsvector('simple', title), 'A')
                    || setweight(to_tsvector('simple', left(coalesce(content, ''), 100000)), 'B')
                ) STORED;
                CREATE INDEX ix_doc_files_search ON github.doc_files USING gin (search);
                """);

            migrationBuilder.EnableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.DocTreesTable);
            migrationBuilder.EnableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.DocFilesTable);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.DocFilesTable);
            migrationBuilder.DisableTenantRowLevelSecurity(GitHubDbContext.Schema, GitHubDbContext.DocTreesTable);

            migrationBuilder.DropTable(
                name: "doc_files",
                schema: "github");

            migrationBuilder.DropTable(
                name: "doc_trees",
                schema: "github");
        }
    }
}
