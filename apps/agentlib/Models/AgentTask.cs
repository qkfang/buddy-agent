using System.Text.Json.Serialization;

namespace BuddyAgent.AgentLib.Models;

public record AgentTask(
    [property: JsonPropertyName("id")]       string  Id,
    [property: JsonPropertyName("url")]      string  Url,
    [property: JsonPropertyName("prompt")]   string  Prompt,
    [property: JsonPropertyName("schedule")] string? Schedule = null
);
