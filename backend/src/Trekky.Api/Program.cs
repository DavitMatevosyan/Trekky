using Microsoft.AspNetCore.Authorization;
using Trekky.Modules.Identity;
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

// Modules (Projects, WorkItems, ...) are added here as they are built.
builder.Services.AddModules(builder.Configuration, new IdentityModule());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseTenantContext(); // after authentication: reads the org_id claim
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
