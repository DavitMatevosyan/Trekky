using Trekky.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Modules (Identity, Projects, WorkItems, ...) are registered here as they are built.
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");

api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Liveness check");

api.MapModules();

await app.RunAsync();

internal sealed record HealthResponse(string Status);

public partial class Program;
