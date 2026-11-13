using Microsoft.AspNetCore.Authorization;
using Trekky.Modules.Activity.Api;
using Trekky.Modules.Identity.Api;
using Trekky.Modules.Projects.Api;
using Trekky.Modules.WorkItems.Api;
using Trekky.Persistence.Registration;
using Trekky.Persistence.Tenancy;
using Trekky.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddTrekkyPersistence();

// Every endpoint needs an authenticated user unless it opts out with AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Browser origins other than the API's own that may call it (ADR-0017), e.g. the desktop app's http://tauri.localhost.
// None by default: the web app is served from the API's origin. Bearer tokens only, so no credentials (cookies).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .WithMethods("GET", "POST", "PATCH", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "If-Match")
    .WithExposedHeaders("ETag", "Location")));

// Modules are added here as they are built.
builder.Services.AddModules(
    builder.Configuration, new IdentityModule(), new ProjectsModule(), new WorkItemsModule(), new ActivityModule());

var app = builder.Build();

// Outside Development, migrations are a deploy step: `Trekky.Api --migrate` applies them as trekky_owner and exits
// (the `migrate` service in deploy/docker-compose.yml runs it before the API starts; a Container Apps job on Azure).
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await app.MigrateModuleDatabasesAsync();
    return;
}

// A request the endpoint cannot bind (e.g. ?limit=abc) throws BadHttpRequestException in Development; answer with its
// status (400) as problem details instead of a 500.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

// Before authentication, so preflight requests (OPTIONS) are answered without a token.
app.UseCors();

app.UseAuthentication();
app.UseTenantContext(); // after authentication: reads the org_id claim

// The fallback authorization policy also applies when no endpoint matched, which would turn unknown URLs into 401.
// Answer 404 instead (problem details via UseStatusCodePages). This runs after authentication, so OpenIddict has
// already served its own endpoints (discovery, JWKS, revoke); a wrong method on a real route still gets 405.
app.Use((context, next) =>
{
    if (context.GetEndpoint() is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    return next(context);
});

app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    await app.MigrateModuleDatabasesAsync();
}

var api = app.MapGroup("/api/v1");

api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .AllowAnonymous()
    .WithName("GetHealth")
    .WithSummary("Liveness check");

api.MapModules();
app.MapModuleRootEndpoints();

await app.RunAsync();

internal sealed record HealthResponse(string Status);

public partial class Program;
