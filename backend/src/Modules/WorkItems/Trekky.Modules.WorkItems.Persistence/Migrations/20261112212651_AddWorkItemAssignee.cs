using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assignee_id",
                schema: "work_items",
                table: "work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_items_org_assignee",
                schema: "work_items",
                table: "work_items",
                columns: new[] { "org_id", "assignee_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_items_org_assignee",
                schema: "work_items",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "assignee_id",
                schema: "work_items",
                table: "work_items");
        }
    }
}
