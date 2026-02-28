using BuddyAgent.AgentLib.Models;
using Microsoft.EntityFrameworkCore;

namespace BuddyAgent.Web.Data;

public class AgentDbContext(DbContextOptions<AgentDbContext> options) : DbContext(options)
{
    public DbSet<TaskRunRecord> TaskRunResults => Set<TaskRunRecord>();
    public DbSet<TaskRecord> Tasks => Set<TaskRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskRunRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RunId).IsRequired().HasMaxLength(36);
            e.Property(x => x.TaskId).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).IsRequired().HasMaxLength(50);
            e.Property(x => x.Url).HasMaxLength(2000);
            e.Property(x => x.Prompt).HasMaxLength(4000);
            e.Property(x => x.Response).HasMaxLength(8000);
            e.Property(x => x.Error).HasMaxLength(2000);
        });

        modelBuilder.Entity<TaskRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(200);
            e.Property(x => x.Url).HasMaxLength(2000);
            e.Property(x => x.Schedule).HasMaxLength(100);
            e.Property(x => x.Content).HasMaxLength(8000);
        });
    }
}

public class TaskRunRecord
{
    public int Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }

    public static TaskRunRecord FromResult(TaskRunResult r) => new()
    {
        RunId     = r.RunId,
        Timestamp = r.Timestamp,
        TaskId    = r.TaskId,
        Url       = r.Url,
        Prompt    = r.Prompt,
        Response  = r.Response,
        Status    = r.Status,
        Error     = r.Error
    };
}

public class TaskRecord
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string Content { get; set; } = string.Empty;
}
