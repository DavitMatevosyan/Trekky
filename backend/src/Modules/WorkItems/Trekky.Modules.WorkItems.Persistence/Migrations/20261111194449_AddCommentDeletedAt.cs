using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "work_items",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);

            // xmin is Postgres's system column (the comment's ETag, as for work items); it exists on every row already.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "work_items",
                table: "comments");
        }
    }
}
