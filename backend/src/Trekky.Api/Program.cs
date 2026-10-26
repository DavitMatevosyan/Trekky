using Trekky.Persistence.Registration;
using Trekky.Persistence.Tenancy;
using Trekky.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddTrekkyPersistence();

// Modules (Identity, Projects, WorkItems, ...) are registered here as they are built.
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Authentication arrives in TRK-7; the tenant must be set after it.
app.UseTenantContext();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.MigrateModuleDatabasesAsync();
}

var api = app.MapGroup("/api/v1");

api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Liveness check");

api.MapModules();

await app.RunAsync();

internal sealed record HealthResponse(string Status);

public partial class Program;
