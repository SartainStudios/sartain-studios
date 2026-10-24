using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public sealed class GigSettings(HttpClient httpClient)
{
    private const string Route = "api/gig/settings";

    public async Task<Preferences> GetAsync()
    {
        var response = await httpClient.GetAsync(UserTimeZone.Append(Route));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Preferences>()
               ?? throw new InvalidOperationException("Empty settings response.");
    }

    public async Task<Preferences> UpdateAsync(UpdatePreferencesRequest request)
    {
        var response = await httpClient.PutAsJsonAsync(UserTimeZone.Append(Route), request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Preferences>()
               ?? throw new InvalidOperationException("Empty settings response.");
    }
}