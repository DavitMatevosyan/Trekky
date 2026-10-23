using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Validation.AspNetCore;
using Trekky.Modules.Identity.Auth;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using Trekky.Persistence.Registration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity;

internal static class IdentityServiceCollectionExtensions
{
    public const string SessionCookieName = "trekky.session";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const int MaxFailedLoginAttempts = 5;

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IdentityModuleOptions.SectionName);
        var options = section.Get<IdentityModuleOptions>() ?? new IdentityModuleOptions();
        services.Configure<IdentityModuleOptions>(section);

        services.TryAddSingleton(TimeProvider.System);
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityModule.Schema);

        // API requests authenticate with the bearer access token; the Identity cookie is used only by
        // login/sign-up (to set it) and /connect/authorize (to read it).
        services
            .AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = SessionCookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = options.AllowInsecureHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
            cookie.SlidingExpiration = true;

            // An API never redirects to a login page.
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            cookie.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services
            .AddIdentityCore<User>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.User.AllowedUserNameCharacters = string.Empty; // the user name is the email
                identity.Password.RequiredLength = SignUpValidation.PasswordMinLength;
                identity.Password.RequiredUniqueChars = 1;
                identity.Password.RequireDigit = false;
                identity.Password.RequireLowercase = false;
                identity.Password.RequireUppercase = false;
                identity.Password.RequireNonAlphanumeric = false;
                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.MaxFailedAccessAttempts = MaxFailedLoginAttempts;
                identity.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<IdentityDbContext>();

        services
            .AddOpenIddict()
            .AddCore(core => core
                .UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(server =>
            {
                server
                    .SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revoke");

                server
                    .AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange();

                server.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.OfflineAccess);

                server
                    .SetAccessTokenLifetime(AccessTokenLifetime)
                    .SetRefreshTokenLifetime(RefreshTokenLifetime)
                    // Refresh tokens rotate on every use (OpenIddict's default). No reuse grace period:
                    // presenting a redeemed refresh token revokes the whole authorization (ADR-0003).
                    .SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                // Access tokens are signed JWTs (RS256, JWKS), not encrypted, so clients and later services can read them.
                server.DisableAccessTokenEncryption();

                if (options.UseDevelopmentCertificates)
                {
                    server.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
                }
                else
                {
                    server
                        .AddEncryptionCertificate(LoadCertificate(options.EncryptionCertificate, nameof(options.EncryptionCertificate)))
                        .AddSigningCertificate(LoadCertificate(options.SigningCertificate, nameof(options.SigningCertificate)));
                }

                var aspNetCore = server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
                if (options.AllowInsecureHttp)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();

                // Check every access token against the token store, so logout revokes it at once
                // instead of leaving it valid for up to 15 minutes.
                validation.EnableTokenEntryValidation();
                validation.EnableAuthorizationEntryValidation();
            });

        services.AddScoped<AccountService>();
        services.AddScoped<MembershipReader>();
        services.AddScoped<SessionRevoker>();
        services.AddHostedService<WebClientRegistration>();

        return services;
    }

    private static X509Certificate2 LoadCertificate(CertificateOptions? certificate, string name)
    {
        if (certificate is null || string.IsNullOrWhiteSpace(certificate.Path))
        {
            throw new InvalidOperationException(
                $"{IdentityModuleOptions.SectionName}:{name}:Path is not configured. Outside development, configure a PKCS#12 " +
                $"certificate (path and password); in development set {IdentityModuleOptions.SectionName}:UseDevelopmentCertificates.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(certificate.Path, certificate.Password);
    }
}
