using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Outbox;

#nullable disable

namespace Trekky.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitesAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invites",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invites", x => x.id);
                    table.CheckConstraint("ck_invites_role", "role IN ('admin', 'member', 'viewer')");
                    table.CheckConstraint("ck_invites_token_hash", "octet_length(token_hash) = 32");
                    table.ForeignKey(
                        name: "fk_invites_organizations_org_id",
                        column: x => x.org_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invites_users_accepted_user_id",
                        column: x => x.accepted_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invites_users_invited_by",
                        column: x => x.invited_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "identity",
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

            migrationBuilder.CreateIndex(
                name: "ix_invites_accepted_user_id",
                schema: "identity",
                table: "invites",
                column: "accepted_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_invited_by",
                schema: "identity",
                table: "invites",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ux_invites_org_open_email",
                schema: "identity",
                table: "invites",
                columns: new[] { "org_id", "normalized_email" },
                unique: true,
                filter: "accepted_at IS NULL AND revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_invites_token_hash",
                schema: "identity",
                table: "invites",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_org_id",
                schema: "identity",
                table: "outbox",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "identity",
                table: "outbox",
                column: "id",
                filter: "processed_at IS NULL");

            // TREK-47: invites are tenant rows, readable by token hash before a tenant is known (ADR-0027); the outbox
            // carries invite events to the activity log (ADR-0011).
            migrationBuilder.EnableInviteRowLevelSecurity();
            migrationBuilder.EnableOutboxRowLevelSecurity(IdentityDbContext.Schema);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableOutboxRowLevelSecurity(IdentityDbContext.Schema);
            migrationBuilder.DisableInviteRowLevelSecurity();

            migrationBuilder.DropTable(
                name: "invites",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "identity");
        }
    }
}
