using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Domain;
using Trekky.Persistence.Outbox;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// The identity schema: Identity's user tables (no roles — roles live on memberships),
/// organizations, memberships, invites, the module's outbox and OpenIddict's tables.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserContext<User, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invite> Invites => Set<Invite>();

    /// <summary>At most one open (not accepted, not revoked) invite per address per organization.</summary>
    public const string OpenInviteIndexName = "ux_invites_org_open_email";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);
        builder.UseOpenIddict<Guid>();
        builder.AddOutbox();

        builder.Entity<User>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();

            // Identity's email index is not unique; sign-up relies on the database to reject a duplicate
            // that slips past the UserManager check in a race.
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ux_users_normalized_email").IsUnique();
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name").IsUnique();
        });

        builder.Entity<Organization>(organization =>
        {
            organization.ToTable("organizations");
            organization.HasKey(o => o.Id);
            organization.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength).IsRequired();
        });

        builder.Entity<Membership>(membership =>
        {
            membership.ToTable("memberships", table => table.HasCheckConstraint(
                "ck_memberships_role",
                $"role IN ('{Roles.Owner}', '{Roles.Admin}', '{Roles.Member}', '{Roles.Viewer}')"));
            membership.HasKey(m => new { m.OrgId, m.UserId });
            // One user belongs to exactly one organization (TREK-46). The key stays (org_id, user_id), so this index
            // is all that changes if a user may later join several organizations.
            membership.HasIndex(m => m.UserId).HasDatabaseName("ux_memberships_user_id").IsUnique();
            membership.Property(m => m.Role).HasMaxLength(16).IsRequired();
            membership.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrgId).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invite>(invite =>
        {
            invite.ToTable(InviteRowLevelSecurity.InvitesTable, table =>
            {
                table.HasCheckConstraint("ck_invites_role", $"role IN ('{Roles.Admin}', '{Roles.Member}', '{Roles.Viewer}')");
                table.HasCheckConstraint("ck_invites_token_hash", "octet_length(token_hash) = 32");
            });
            invite.HasKey(i => i.Id);
            invite.Property(i => i.Id).ValueGeneratedNever();
            invite.Property(i => i.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
            invite.Property(i => i.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
            invite.Property(i => i.Role).HasMaxLength(16).IsRequired();
            invite.Property(i => i.TokenHash).IsRequired();
            invite.Property(i => i.Version).IsRowVersion();
            invite.HasIndex(i => i.TokenHash).HasDatabaseName("ux_invites_token_hash").IsUnique();
            invite.HasIndex(i => new { i.OrgId, i.NormalizedEmail })
                .HasDatabaseName(OpenInviteIndexName)
                .IsUnique()
                .HasFilter("accepted_at IS NULL AND revoked_at IS NULL");
            invite.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrgId).OnDelete(DeleteBehavior.Cascade);
            invite.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedBy).OnDelete(DeleteBehavior.Restrict);
            invite.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedUserId).OnDelete(DeleteBehavior.Restrict);
        });

        RenameFrameworkTables(builder);
    }

    // Identity (AspNetUsers, ...) and OpenIddict (OpenIddictTokens, ...) set PascalCase table names explicitly,
    // which the snake_case convention leaves alone. Rename them to users, user_claims, openiddict_tokens, ...
    private static void RenameFrameworkTables(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null)
            {
                continue;
            }

            if (table.StartsWith("AspNet", StringComparison.Ordinal))
            {
                entity.SetTableName(ToSnakeCase(table["AspNet".Length..]));
            }
            else if (table.StartsWith("OpenIddict", StringComparison.Ordinal))
            {
                entity.SetTableName("openiddict_" + ToSnakeCase(table["OpenIddict".Length..]));
            }
        }

        // Foreign key names were derived from the original principal table (fk_memberships_asp_net_users_user_id);
        // name them after the renamed table instead (fk_memberships_users_user_id). The names are now explicit,
        // so they will not follow a later rename of the principal table.
        foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            var name = foreignKey.GetConstraintName();
            if (name is not null && name.Contains("_asp_net_", StringComparison.Ordinal))
            {
                foreignKey.SetConstraintName(name.Replace("_asp_net_", "_", StringComparison.Ordinal));
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }
}
