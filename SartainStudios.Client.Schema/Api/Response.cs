using System.Net.Http.Json;
using System.Text.Json;

namespace SartainStudios.Client.Schema.Api;

public static class Response
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var problem = await ReadProblemAsync(response);
        throw new Exception(problem, response.StatusCode);
    }

    private static async Task<Problem> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>();
            if (problem is not null && (problem.Detail is not null || problem.Title is not null ||
                                        problem.Errors is { Count: > 0 }))
                return problem;
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException)
        {
            body = string.Empty;
        }

        return new Problem
        {
            Status = (int)response.StatusCode,
            Detail = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "Request failed."
                : body
        };
    }
}