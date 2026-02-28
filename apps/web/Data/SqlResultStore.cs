using BuddyAgent.AgentLib.Models;
using BuddyAgent.AgentLib.Storage;
using Microsoft.EntityFrameworkCore;

namespace BuddyAgent.Web.Data;

public class SqlResultStore(IDbContextFactory<AgentDbContext> factory) : IResultStore
{
    public async Task AddAsync(TaskRunResult result)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.TaskRunResults.Add(TaskRunRecord.FromResult(result));
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TaskRunResult>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.TaskRunResults
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync();
        return rows.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<TaskRunResult>> GetByTaskIdAsync(string taskId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.TaskRunResults
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync();
        return rows.Select(ToModel).ToList();
    }

    private static TaskRunResult ToModel(TaskRunRecord r) => new(
        RunId:     r.RunId,
        Timestamp: r.Timestamp,
        TaskId:    r.TaskId,
        Url:       r.Url,
        Prompt:    r.Prompt,
        Response:  r.Response,
        Status:    r.Status,
        Error:     r.Error
    );
}
