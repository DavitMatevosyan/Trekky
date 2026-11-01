using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "work_items");

            migrationBuilder.CreateTable(
                name: "item_counters",
                schema: "work_items",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_counters", x => x.project_id);
                    table.CheckConstraint("ck_item_counters_next", "next_number > 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "work_items",
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
                name: "work_items",
                schema: "work_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description_md = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_items", x => x.id);
                    table.CheckConstraint("ck_work_items_custom", "jsonb_typeof(custom) = 'object'");
                    table.CheckConstraint("ck_work_items_description", "char_length(description_md) <= 50000");
                    table.CheckConstraint("ck_work_items_number", "number > 0");
                    table.CheckConstraint("ck_work_items_status", "status IN ('todo', 'in_progress', 'in_review', 'done')");
                    table.CheckConstraint("ck_work_items_title", "char_length(title) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_work_items_type", "type IN ('epic', 'story', 'task', 'subtask', 'bug', 'tech_debt')");
                    table.ForeignKey(
                        name: "fk_work_items_work_items_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "work_items",
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_counters_org_id",
                schema: "work_items",
                table: "item_counters",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_org_id",
                schema: "work_items",
                table: "outbox",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "work_items",
                table: "outbox",
                column: "id",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_org_project_updated",
                schema: "work_items",
                table: "work_items",
                columns: new[] { "org_id", "project_id", "updated_at", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_parent",
                schema: "work_items",
                table: "work_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_work_items_org_key",
                schema: "work_items",
                table: "work_items",
                columns: new[] { "org_id", "project_key", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_work_items_project_number",
                schema: "work_items",
                table: "work_items",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity(WorkItemsDbContext.Schema, WorkItemsDbContext.WorkItemsTable);
            migrationBuilder.EnableTenantRowLevelSecurity(WorkItemsDbContext.Schema, WorkItemsDbContext.ItemCountersTable);
            migrationBuilder.EnableOutboxRowLevelSecurity(WorkItemsDbContext.Schema);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableOutboxRowLevelSecurity(WorkItemsDbContext.Schema);
            migrationBuilder.DisableTenantRowLevelSecurity(WorkItemsDbContext.Schema, WorkItemsDbContext.ItemCountersTable);
            migrationBuilder.DisableTenantRowLevelSecurity(WorkItemsDbContext.Schema, WorkItemsDbContext.WorkItemsTable);

            migrationBuilder.DropTable(
                name: "item_counters",
                schema: "work_items");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "work_items");

            migrationBuilder.DropTable(
                name: "work_items",
                schema: "work_items");
        }
    }
}
