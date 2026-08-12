using System.Text.Json.Serialization;

namespace SartainStudios.Client.Schema.Api;

public sealed record Problem
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
    [JsonPropertyName("status")] public int? Status { get; init; }
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("errors")] public Dictionary<string, string[]>? Errors { get; init; }

    public string ToMessage()
    {
        if (Errors is { Count: > 0 })
        {
            var messages = Errors
                .SelectMany(entry => entry.Value)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
            if (messages.Length > 0) return string.Join(" ", messages);
        }

        if (!string.IsNullOrWhiteSpace(Detail)) return Detail!;
        return !string.IsNullOrWhiteSpace(Title) ? Title! : "Request failed.";
    }
}