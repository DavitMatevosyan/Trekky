using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trekky.Modules.Identity.Contracts;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;

namespace Trekky.Modules.Identity.Auth;

internal enum SignUpOutcome
{
    Created,
    EmailTaken,
}

internal enum LoginOutcome
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}

/// <summary>Sign-up and login (US-01, US-02). Both end with the HttpOnly session cookie that <c>/connect/authorize</c> reads.</summary>
internal sealed class AccountService(
    IdentityDbContext db,
    UserManager<User> users,
    SignInManager<User> signIn,
    TimeProvider time)
{
    /// <summary>
    /// Creates the user, the organization and the Owner membership in one transaction (US-01 AC5),
    /// then signs the user in.
    /// </summary>
    public async Task<(SignUpOutcome Outcome, MeResponse? Me)> SignUpAsync(SignUpInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var now = time.GetUtcNow();
        var organization = new Organization { Id = Guid.CreateVersion7(), Name = input.OrganizationName, CreatedAt = now };
        var user = new User { Email = input.Email, UserName = input.Email, DisplayName = input.DisplayName, CreatedAt = now };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // RLS: the new organization and its membership are written as that organization.
        await db.SetTenantAsync(organization.Id, cancellationToken);

        try
        {
            var created = await users.CreateAsync(user, input.Password);
            if (!created.Succeeded)
            {
                if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                                                   or nameof(IdentityErrorDescriber.DuplicateUserName)))
                {
                    return (SignUpOutcome.EmailTaken, null);
                }

                // Input was validated up front, so anything else is a bug, not a user error.
                throw new InvalidOperationException(
                    "Creating the user failed: " + string.Join(", ", created.Errors.Select(e => e.Code)));
            }

            db.Organizations.Add(organization);
            db.Memberships.Add(new Membership
            {
                OrgId = organization.Id,
                UserId = user.Id,
                Role = Roles.Owner,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent sign-up with the same email won the race.
            return (SignUpOutcome.EmailTaken, null);
        }

        await signIn.SignInAsync(user, isPersistent: false);

        return (SignUpOutcome.Created, new MeResponse(
            new UserRef(user.Id, user.DisplayName, user.Email),
            new OrganizationRef(organization.Id, organization.Name),
            Roles.Owner));
    }

    /// <summary>Checks the password with lockout (5 failures → 15 minutes) and sets the session cookie.</summary>
    public async Task<LoginOutcome> LogInAsync(string email, string password)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return LoginOutcome.InvalidCredentials;
        }

        var result = await signIn.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return LoginOutcome.Succeeded;
        }

        return result.IsLockedOut ? LoginOutcome.LockedOut : LoginOutcome.InvalidCredentials;
    }

    /// <summary>The current user, organization and role, read as the request's tenant.</summary>
    public async Task<MeResponse?> GetMeAsync(Guid userId, Guid orgId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserRef(u.Id, u.DisplayName, u.Email))
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var membership = await (
                from m in db.Memberships.AsNoTracking()
                join o in db.Organizations.AsNoTracking() on m.OrgId equals o.Id
                where m.UserId == userId && m.OrgId == orgId
                select new { o.Id, o.Name, m.Role })
            .FirstOrDefaultAsync(cancellationToken);

        return membership is null
            ? null
            : new MeResponse(user, new OrganizationRef(membership.Id, membership.Name), membership.Role);
    }
}
