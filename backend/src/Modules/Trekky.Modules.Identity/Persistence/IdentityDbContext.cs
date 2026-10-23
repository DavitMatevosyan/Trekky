using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// The identity schema: Identity's user tables (no roles — roles live on memberships),
/// organizations, memberships and OpenIddict's tables.
/// </summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserContext<User, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(IdentityModule.Schema);
        builder.UseOpenIddict<Guid>();

        builder.Entity<User>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(SignUpValidation.DisplayNameMaxLength).IsRequired();

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
            membership.HasIndex(m => m.UserId);
            membership.Property(m => m.Role).HasMaxLength(16).IsRequired();
            membership.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrgId).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
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
