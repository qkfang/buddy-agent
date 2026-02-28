using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuddyAgent.AgentLib.Models;
using BuddyAgent.AgentLib.Storage;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuddyAgent.AgentLib;

public class AgentRunner
{
    private readonly IResultStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentRunner> _logger;

    public AgentRunner(IResultStore store, IConfiguration config, ILogger<AgentRunner> logger)
    {
        _store  = store;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskRunResult>> RunAllTasksAsync(
        string tasksPath, CancellationToken cancellationToken = default)
    {
        string githubToken = _config["GITHUB_TOKEN"]
            ?? throw new InvalidOperationException(
                "GITHUB_TOKEN is not set.");
        string modelName = _config["GITHUB_MODEL"] ?? "gpt-4o-mini";

        if (!File.Exists(tasksPath))
            throw new FileNotFoundException($"Tasks file not found: {tasksPath}");

        var tasksJson = await File.ReadAllTextAsync(tasksPath, cancellationToken);
        var taskFiles = JsonSerializer.Deserialize<List<string>>(tasksJson, JsonOpts.Default)
            ?? throw new InvalidOperationException("tasks.json is empty or invalid.");

        var tasksDir = Path.GetDirectoryName(Path.GetFullPath(tasksPath)) ?? ".";

        var tasks = new List<AgentTask>();
        foreach (var relPath in taskFiles)
        {
            var mdPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(tasksDir, relPath);
            if (!File.Exists(mdPath))
            {
                _logger.LogWarning("Markdown task file not found, skipping: {Path}", mdPath);
                continue;
            }
            var mdContent = await File.ReadAllTextAsync(mdPath, cancellationToken);
            tasks.Add(ParseMarkdownTask(mdPath, mdContent));
        }

        _logger.LogInformation("Loaded {Count} task(s)", tasks.Count);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BuddyAgent/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);

        await using var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = githubToken
        });
        await copilotClient.StartAsync();

        var results = new List<TaskRunResult>();

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Running task {TaskId}", task.Id);

            string? urlContent = null;
            if (!string.IsNullOrWhiteSpace(task.Url))
            {
                try
                {
                    urlContent = await http.GetStringAsync(task.Url, cancellationToken);
                    if (urlContent.Length > 8000)
                        urlContent = urlContent[..8000] + "\n[... content truncated ...]";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not fetch URL {Url}: {Error}", task.Url, ex.Message);
                }
            }

            var userMessage = new StringBuilder();
            if (urlContent is not null)
            {
                userMessage.AppendLine("The following content was fetched from the URL provided:");
                userMessage.AppendLine($"URL: {task.Url}");
                userMessage.AppendLine();
                userMessage.AppendLine("--- BEGIN CONTENT ---");
                userMessage.AppendLine(urlContent);
                userMessage.AppendLine("--- END CONTENT ---");
                userMessage.AppendLine();
            }
            userMessage.Append(task.Prompt);

            string response;
            string status;
            string? error = null;

            try
            {
                await using var session = await copilotClient.CreateSessionAsync(new SessionConfig
                {
                    Model               = modelName,
                    OnPermissionRequest = PermissionHandler.ApproveAll,
                    SystemMessage       = new SystemMessageConfig
                    {
                        Mode    = SystemMessageMode.Replace,
                        Content = "You are a helpful assistant. When URL content is provided, " +
                                  "use it as context to answer the user's question accurately."
                    }
                });

                var responseBuilder = new StringBuilder();
                var done = new TaskCompletionSource();

                session.On(evt =>
                {
                    switch (evt)
                    {
                        case AssistantMessageEvent msg:
                            responseBuilder.Append(msg.Data.Content);
                            break;
                        case SessionErrorEvent err:
                            done.TrySetException(new Exception(err.Data.Message));
                            break;
                        case SessionIdleEvent:
                            done.TrySetResult();
                            break;
                    }
                });

                await session.SendAsync(new MessageOptions { Prompt = userMessage.ToString() });
                await done.Task;

                response = responseBuilder.ToString();
                status   = "success";
                _logger.LogInformation("Task {TaskId} completed successfully", task.Id);
            }
            catch (Exception ex)
            {
                response = string.Empty;
                status   = "error";
                error    = ex.Message;
                _logger.LogError(ex, "Task {TaskId} failed", task.Id);
            }

            var result = new TaskRunResult(
                RunId:     Guid.NewGuid().ToString(),
                Timestamp: DateTimeOffset.UtcNow,
                TaskId:    task.Id,
                Url:       task.Url,
                Prompt:    task.Prompt,
                Response:  response,
                Status:    status,
                Error:     error
            );

            await _store.AddAsync(result);
            results.Add(result);
        }

        return results;
    }

    private static AgentTask ParseMarkdownTask(string filePath, string content)
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
}

file static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
