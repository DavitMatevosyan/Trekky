using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trekky.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueMembershipPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_memberships_user_id",
                schema: "identity",
                table: "memberships");

            migrationBuilder.CreateIndex(
                name: "ux_memberships_user_id",
                schema: "identity",
                table: "memberships",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_memberships_user_id",
                schema: "identity",
                table: "memberships");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id",
                schema: "identity",
                table: "memberships",
                column: "user_id");
        }
    }
}
