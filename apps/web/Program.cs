using BuddyAgent.AgentLib;
using BuddyAgent.AgentLib.Storage;
using BuddyAgent.Web.Data;
using BuddyAgent.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Persistence: SQL Server if connection string is set, otherwise in-memory ────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContextFactory<AgentDbContext>(opts =>
        opts.UseSqlServer(connectionString));
    builder.Services.AddScoped<IResultStore, SqlResultStore>();
}
else
{
    // In-memory store for local development / testing
    builder.Services.AddSingleton<IResultStore, InMemoryResultStore>();
}

// ── Agent runner (scoped so it can take scoped deps like SqlResultStore) ─────────
builder.Services.AddScoped<AgentRunner>();

// ── Hourly background service ────────────────────────────────────────────────────
builder.Services.AddHostedService<AgentBackgroundService>();

// ── OpenAPI (endpoint discovery) ─────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Database migration on startup (only when SQL Server is configured) ───────────
if (!string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AgentDbContext>>();
    await using var ctx = await db.CreateDbContextAsync();
    await ctx.Database.MigrateAsync();
}

// ── Static files (frontend) ──────────────────────────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

// ── API: GET /api/results ────────────────────────────────────────────────────────
app.MapGet("/api/results", async (IResultStore store) =>
    Results.Ok(await store.GetAllAsync()))
    .WithName("GetAllResults");

// ── API: GET /api/results/{taskId} ───────────────────────────────────────────────
app.MapGet("/api/results/{taskId}", async (string taskId, IResultStore store) =>
    Results.Ok(await store.GetByTaskIdAsync(taskId)))
    .WithName("GetResultsByTaskId");

// ── Health check ─────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

app.Run();
