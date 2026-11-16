using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.Activity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "activity");

            migrationBuilder.CreateTable(
                name: "activity_events",
                schema: "activity",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_events", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_events_entity",
                schema: "activity",
                table: "activity_events",
                columns: new[] { "org_id", "entity_id", "event_id" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_activity_events_project",
                schema: "activity",
                table: "activity_events",
                columns: new[] { "org_id", "project_id", "event_id" },
                descending: new[] { false, false, true });

            migrationBuilder.EnableTenantRowLevelSecurity(ActivityDbContext.Schema, ActivityDbContext.EventsTable);

            // Append-only (docs/tech/data-model.md): the app role may insert and read, never change or remove history.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE, TRUNCATE ON activity.activity_events FROM trekky_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRowLevelSecurity(ActivityDbContext.Schema, ActivityDbContext.EventsTable);

            migrationBuilder.DropTable(
                name: "activity_events",
                schema: "activity");
        }
    }
}
