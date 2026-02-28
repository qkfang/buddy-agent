using BuddyAgent.AgentLib.Models;

namespace BuddyAgent.AgentLib.Storage;

public interface IResultStore
{
    Task AddAsync(TaskRunResult result);
    Task<IReadOnlyList<TaskRunResult>> GetAllAsync();
    Task<IReadOnlyList<TaskRunResult>> GetByTaskIdAsync(string taskId);
}
