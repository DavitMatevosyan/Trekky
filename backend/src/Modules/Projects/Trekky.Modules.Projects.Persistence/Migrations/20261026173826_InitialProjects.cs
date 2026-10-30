using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.Projects.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "projects",
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
                name: "projects",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.CheckConstraint("ck_projects_key", "key ~ '^[A-Z][A-Z0-9]{1,9}$'");
                    table.CheckConstraint("ck_projects_name", "char_length(name) BETWEEN 1 AND 80");
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_org_id",
                schema: "projects",
                table: "outbox",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "projects",
                table: "outbox",
                column: "id",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projects_org_name",
                schema: "projects",
                table: "projects",
                columns: new[] { "org_id", "name", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_projects_org_key",
                schema: "projects",
                table: "projects",
                columns: new[] { "org_id", "key" },
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity(ProjectsDbContext.Schema, ProjectsDbContext.ProjectsTable);
            migrationBuilder.EnableOutboxRowLevelSecurity(ProjectsDbContext.Schema);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableOutboxRowLevelSecurity(ProjectsDbContext.Schema);
            migrationBuilder.DisableTenantRowLevelSecurity(ProjectsDbContext.Schema, ProjectsDbContext.ProjectsTable);

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "projects");
        }
    }
}
