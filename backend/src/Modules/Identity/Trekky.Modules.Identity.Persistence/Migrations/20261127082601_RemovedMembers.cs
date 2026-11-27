using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovedMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "removed_at",
                schema: "identity",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            // xmin is Postgres's system column (the member's ETag, TREK-48); it exists on every row already.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "removed_at",
                schema: "identity",
                table: "memberships");
        }
    }
}
