using System.Text.Json;
using BuddyAgent.AgentLib;
using BuddyAgent.AgentLib.Models;
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

// ── API: GET /api/tasks ──────────────────────────────────────────────────────────
app.MapGet("/api/tasks", async (IConfiguration config) =>
{
    var tasksPath = GetTasksPath(config);
    if (!File.Exists(tasksPath))
        return Results.Ok(Array.Empty<AgentTask>());

    var tasksJson = await File.ReadAllTextAsync(tasksPath);
    var taskFiles = JsonSerializer.Deserialize<List<string>>(tasksJson, JsonOpts.Default) ?? [];
    var tasksDir  = Path.GetDirectoryName(Path.GetFullPath(tasksPath)) ?? ".";

    var tasks = new List<AgentTask>();
    foreach (var relPath in taskFiles)
    {
        var mdPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(tasksDir, relPath);
        if (!File.Exists(mdPath)) continue;
        var mdContent = await File.ReadAllTextAsync(mdPath);
        tasks.Add(ParseMarkdownTask(mdPath, mdContent));
    }
    return Results.Ok(tasks);
})
.WithName("GetAllTasks");

// ── API: POST /api/tasks ─────────────────────────────────────────────────────────
app.MapPost("/api/tasks", async (CreateTaskRequest req, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(req.Id))
        return Results.BadRequest(new { error = "Task id is required." });
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "Prompt is required." });

    // Sanitise id: allow ASCII letters, digits, hyphens and underscores only
    var safeId = System.Text.RegularExpressions.Regex.Replace(req.Id.Trim(), @"[^a-zA-Z0-9_\-]", "-");

    var tasksPath = GetTasksPath(config);
    var tasksDir  = Path.GetDirectoryName(Path.GetFullPath(tasksPath)) ?? ".";
    var mdDir     = Path.Combine(tasksDir, "tasks");
    Directory.CreateDirectory(mdDir);

    var mdFileName = $"{safeId}.md";
    var mdPath     = Path.Combine(mdDir, mdFileName);
    if (File.Exists(mdPath))
        return Results.Conflict(new { error = $"Task '{safeId}' already exists." });

    // Write markdown file with YAML front matter
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("---");
    sb.AppendLine($"id: {safeId}");
    if (!string.IsNullOrWhiteSpace(req.Url))
        sb.AppendLine($"url: {req.Url}");
    if (!string.IsNullOrWhiteSpace(req.Schedule))
        sb.AppendLine($"schedule: {req.Schedule}");
    sb.AppendLine("---");
    sb.AppendLine(req.Prompt.Trim());
    await File.WriteAllTextAsync(mdPath, sb.ToString());

    // Update tasks.json
    List<string> taskFiles;
    if (File.Exists(tasksPath))
    {
        var existing = await File.ReadAllTextAsync(tasksPath);
        taskFiles = JsonSerializer.Deserialize<List<string>>(existing, JsonOpts.Default) ?? [];
    }
    else
    {
        taskFiles = [];
    }

    var relMdPath = Path.GetRelativePath(tasksDir, mdPath)
                        .Replace(Path.DirectorySeparatorChar, '/');
    if (!taskFiles.Contains(relMdPath))
        taskFiles.Add(relMdPath);

    await File.WriteAllTextAsync(tasksPath,
        JsonSerializer.Serialize(taskFiles, JsonOpts.Default));

    var created = new AgentTask(safeId, req.Url?.Trim() ?? string.Empty, req.Prompt.Trim(), req.Schedule?.Trim());
    return Results.Created($"/api/tasks/{safeId}", created);
})
.WithName("CreateTask");

// ── Health check ─────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// ── DB status: reports whether a SQL connection string is configured ──────────────
app.MapGet("/api/dbstatus", (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DefaultConnection");
    return Results.Ok(new { deployed = !string.IsNullOrWhiteSpace(cs) });
})
.WithName("GetDbStatus");

app.Run();

// ── Helpers ──────────────────────────────────────────────────────────────────────
static string GetTasksPath(IConfiguration config)
{
    var configured = config["AgentLib:TasksPath"];
    return string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(AppContext.BaseDirectory, "tasks.json")
        : configured;
}

static AgentTask ParseMarkdownTask(string filePath, string content)
{
    var id       = Path.GetFileNameWithoutExtension(filePath);
    var url      = string.Empty;
    var schedule = (string?)null;
    var prompt   = content.Trim();

    if (content.StartsWith("---"))
    {
        var end = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (end != -1)
        {
            var frontMatter = content[3..end].Trim();
            prompt = content[(end + 3)..].Trim();

            foreach (var line in frontMatter.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon < 0) continue;
                var key   = line[..colon].Trim().ToLowerInvariant();
                var value = line[(colon + 1)..].Trim();
                if (key == "id")       id       = value;
                if (key == "url")      url      = value;
                if (key == "schedule") schedule = value;
            }
        }
    }
    return new AgentTask(id, url, prompt, schedule);
}

file static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive    = true,
        PropertyNamingPolicy           = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition         = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented                  = true
    };
}

record CreateTaskRequest(string? Id, string? Url, string? Schedule, string? Prompt);
