using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rank",
                schema: "work_items",
                table: "work_items",
                type: "text",
                nullable: false,
                defaultValue: "",
                collation: "C");

            // Existing cards keep the order the board showed: newest update first, Done last finished first. Ranks
            // "c" + three base-62 digits are valid BoardRank integers (238,328 per column). Forced RLS hides every row
            // from the owner too, so the backfill lifts it for these statements; the migration's transaction restores
            // it even on failure.
            migrationBuilder.Sql(
                """
                ALTER TABLE work_items.work_items NO FORCE ROW LEVEL SECURITY;
                WITH digits AS (SELECT '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'::text AS d),
                ordered AS (
                    SELECT id, row_number() OVER (
                        PARTITION BY project_id, status
                        ORDER BY CASE WHEN status = 'done' THEN done_at ELSE updated_at END DESC, id DESC) AS n
                    FROM work_items.work_items)
                UPDATE work_items.work_items w
                SET rank = 'c'
                    || substr(digits.d, (ordered.n / 3844 % 62)::int + 1, 1)
                    || substr(digits.d, (ordered.n / 62 % 62)::int + 1, 1)
                    || substr(digits.d, (ordered.n % 62)::int + 1, 1)
                FROM ordered, digits
                WHERE w.id = ordered.id;
                ALTER TABLE work_items.work_items FORCE ROW LEVEL SECURITY;
                ALTER TABLE work_items.work_items ALTER COLUMN rank DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_work_items_org_project_status_rank",
                schema: "work_items",
                table: "work_items",
                columns: new[] { "org_id", "project_id", "status", "rank" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_rank",
                schema: "work_items",
                table: "work_items",
                sql: "rank <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_work_items_org_project_status_rank",
                schema: "work_items",
                table: "work_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_rank",
                schema: "work_items",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "rank",
                schema: "work_items",
                table: "work_items");
        }
    }
}
