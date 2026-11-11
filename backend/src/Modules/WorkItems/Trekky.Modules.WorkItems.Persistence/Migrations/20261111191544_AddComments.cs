using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

#nullable disable

namespace Trekky.Modules.WorkItems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comments",
                schema: "work_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body_md = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.CheckConstraint("ck_comments_body", "char_length(body_md) BETWEEN 1 AND 20000 AND char_length(btrim(body_md)) > 0");
                    table.ForeignKey(
                        name: "fk_comments_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalSchema: "work_items",
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comments_item_created",
                schema: "work_items",
                table: "comments",
                columns: new[] { "work_item_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_org",
                schema: "work_items",
                table: "comments",
                column: "org_id");

            // Comments are tenant-owned: RLS on org_id like every other table (ADR-0002).
            migrationBuilder.EnableTenantRowLevelSecurity(WorkItemsDbContext.Schema, WorkItemsDbContext.CommentsTable);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments",
                schema: "work_items");
        }
    }
}
