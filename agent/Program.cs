using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.Inference;

// ── Configuration ──────────────────────────────────────────────────────────────
string githubToken  = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                      ?? throw new InvalidOperationException(
                             "GITHUB_TOKEN environment variable is not set.");
string modelName    = Environment.GetEnvironmentVariable("GITHUB_MODEL")
                      ?? "gpt-4o-mini";
string tasksPath    = args.Length > 0 ? args[0] : "tasks.json";
string historyPath  = args.Length > 1 ? args[1] : "history.json";

// ── Load tasks ─────────────────────────────────────────────────────────────────
if (!File.Exists(tasksPath))
    throw new FileNotFoundException($"Tasks file not found: {tasksPath}");

var tasksJson   = await File.ReadAllTextAsync(tasksPath);
var taskFiles   = JsonSerializer.Deserialize<List<string>>(tasksJson, JsonOptions.Default)
                  ?? throw new InvalidOperationException("tasks.json is empty or invalid.");

// Resolve paths relative to the tasks.json file's directory
var tasksDir    = Path.GetDirectoryName(Path.GetFullPath(tasksPath)) ?? ".";

var tasks = new List<AgentTask>();
foreach (var relPath in taskFiles)
{
    var mdPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(tasksDir, relPath);
    if (!File.Exists(mdPath))
    {
        Console.WriteLine($"WARNING: Markdown task file not found, skipping: {mdPath}");
        continue;
    }

    var mdContent = await File.ReadAllTextAsync(mdPath);
    tasks.Add(ParseMarkdownTask(mdPath, mdContent));
}

Console.WriteLine($"Loaded {tasks.Count} task(s) from {tasks.Count} markdown file(s) listed in {tasksPath}");

// ── Load existing history (so runs are cumulative) ─────────────────────────────
List<HistoryEntry> history = [];
if (File.Exists(historyPath))
{
    var existingJson = await File.ReadAllTextAsync(historyPath);
    history = JsonSerializer.Deserialize<List<HistoryEntry>>(existingJson,
                  JsonOptions.Default) ?? [];
    Console.WriteLine($"Loaded {history.Count} existing history entry/entries from {historyPath}");
}

// ── GitHub Models client ───────────────────────────────────────────────────────
var endpoint = new Uri("https://models.inference.ai.azure.com");
var client   = new ChatCompletionsClient(endpoint, new AzureKeyCredential(githubToken));

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("BuddyAgent/1.0");
http.Timeout = TimeSpan.FromSeconds(30);

// ── Process tasks ──────────────────────────────────────────────────────────────
foreach (var task in tasks)
{
    Console.WriteLine($"\n─── Task {task.Id} ───────────────────────────────────────");

    string? urlContent = null;

    // Fetch URL content when provided
    if (!string.IsNullOrWhiteSpace(task.Url))
    {
        Console.WriteLine($"  Fetching URL: {task.Url}");
        try
        {
            urlContent = await http.GetStringAsync(task.Url);
            // Truncate to keep well within token limits (~8k chars ≈ ~2k tokens)
            if (urlContent.Length > 8000)
                urlContent = urlContent[..8000] + "\n[... content truncated ...]";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WARNING: Could not fetch URL – {ex.Message}");
        }
    }

    // Build the user message
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

    Console.WriteLine($"  Prompt: {task.Prompt}");
    Console.WriteLine("  Calling GitHub Models API…");

    string response;
    string status;
    string? error = null;

    try
    {
        var chatRequest = new ChatCompletionsOptions
        {
            Model = modelName,
            Messages =
            {
                new ChatRequestSystemMessage(
                    "You are a helpful assistant. When URL content is provided, "  +
                    "use it as context to answer the user's question accurately."),
                new ChatRequestUserMessage(userMessage.ToString())
            },
            MaxTokens   = 1024,
            Temperature = 0.7f
        };

        var result = await client.CompleteAsync(chatRequest);
        response = result.Value.Content;
        status   = "success";

        Console.WriteLine("  Response received.");
    }
    catch (Exception ex)
    {
        response = string.Empty;
        status   = "error";
        error    = ex.Message;
        Console.WriteLine($"  ERROR: {ex.Message}");
    }

    history.Add(new HistoryEntry(
        RunId:     Guid.NewGuid().ToString(),
        Timestamp: DateTimeOffset.UtcNow,
        TaskId:    task.Id,
        Url:       task.Url,
        Prompt:    task.Prompt,
        Response:  response,
        Status:    status,
        Error:     error
    ));
}

// ── Persist history ────────────────────────────────────────────────────────────
var historyJson = JsonSerializer.Serialize(history, JsonOptions.Pretty);
await File.WriteAllTextAsync(historyPath, historyJson);
Console.WriteLine($"\nHistory written to {historyPath}  ({history.Count} total entries)");

// ── Models ─────────────────────────────────────────────────────────────────────

record AgentTask(
    [property: JsonPropertyName("id")]     string Id,
    [property: JsonPropertyName("url")]    string Url,
    [property: JsonPropertyName("prompt")] string Prompt
);

record HistoryEntry(
    [property: JsonPropertyName("runId")]     string          RunId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset  Timestamp,
    [property: JsonPropertyName("taskId")]    string          TaskId,
    [property: JsonPropertyName("url")]       string          Url,
    [property: JsonPropertyName("prompt")]    string          Prompt,
    [property: JsonPropertyName("response")]  string          Response,
    [property: JsonPropertyName("status")]    string          Status,
    [property: JsonPropertyName("error")]     string?         Error
);

// ── Markdown front-matter parser ──────────────────────────────────────────────
static AgentTask ParseMarkdownTask(string filePath, string content)
{
    var id     = Path.GetFileNameWithoutExtension(filePath);
    var url    = string.Empty;
    var prompt = content.Trim();

    // Detect YAML front matter delimited by ---
    if (content.StartsWith("---"))
    {
        var end = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (end != -1)
        {
            var frontMatter = content[3..end].Trim();
            prompt          = content[(end + 3)..].Trim();

            foreach (var line in frontMatter.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon < 0) continue;
                var key   = line[..colon].Trim().ToLowerInvariant();
                var value = line[(colon + 1)..].Trim();
                if (key == "id")  id  = value;
                if (key == "url") url = value;
            }
        }
    }

    return new AgentTask(id, url, prompt);
}

static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static readonly JsonSerializerOptions Pretty = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
