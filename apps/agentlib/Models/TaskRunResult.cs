using System.Text.Json.Serialization;

namespace BuddyAgent.AgentLib.Models;

public record TaskRunResult(
    [property: JsonPropertyName("runId")]     string         RunId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("taskId")]    string         TaskId,
    [property: JsonPropertyName("url")]       string         Url,
    [property: JsonPropertyName("prompt")]    string         Prompt,
    [property: JsonPropertyName("response")]  string         Response,
    [property: JsonPropertyName("status")]    string         Status,
    [property: JsonPropertyName("error")]     string?        Error
);
