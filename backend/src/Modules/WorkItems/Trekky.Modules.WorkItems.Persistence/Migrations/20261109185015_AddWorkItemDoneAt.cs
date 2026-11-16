using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDoneAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "done_at",
                schema: "work_items",
                table: "work_items",
                type: "timestamp with time zone",
                nullable: true);

            // The best record of when existing items finished. Forced RLS hides every row from the owner too, so the
            // backfill lifts it for this one statement; the migration's transaction restores it even on failure.
            migrationBuilder.Sql(
                """
                ALTER TABLE work_items.work_items NO FORCE ROW LEVEL SECURITY;
                UPDATE work_items.work_items SET done_at = updated_at WHERE status = 'done';
                ALTER TABLE work_items.work_items FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "done_at",
                schema: "work_items",
                table: "work_items");
        }
    }
}
