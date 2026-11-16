using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDoneAtCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_work_items_done_at",
                schema: "work_items",
                table: "work_items",
                sql: "(status = 'done') = (done_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_items_done_at",
                schema: "work_items",
                table: "work_items");
        }
    }
}
