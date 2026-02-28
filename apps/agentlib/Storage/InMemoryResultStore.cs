using BuddyAgent.AgentLib.Models;

namespace BuddyAgent.AgentLib.Storage;

public class InMemoryResultStore : IResultStore
{
    private readonly List<TaskRunResult> _results = [];
    private readonly Lock _lock = new();

    public Task AddAsync(TaskRunResult result)
    {
        lock (_lock)
            _results.Add(result);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TaskRunResult>> GetAllAsync()
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<TaskRunResult>>([.. _results]);
    }

    public Task<IReadOnlyList<TaskRunResult>> GetByTaskIdAsync(string taskId)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<TaskRunResult>>(
                _results.Where(r => r.TaskId == taskId).ToList());
    }
}
