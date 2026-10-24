using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public sealed class GigReport(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<Report> GetAsync(int weeks = 12)
    {
        var response = await httpClient.GetAsync(UserTimeZone.Append($"api/gig/reports?weeks={weeks}"));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Report>(JsonOptions)
               ?? throw new InvalidOperationException("Empty report response.");
    }
}