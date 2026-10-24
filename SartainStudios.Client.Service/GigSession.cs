using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public sealed class GigSession(HttpClient httpClient)
{
    private const string BaseRoute = "api/gig/sessions";

    public async Task<GigSessionState> GetCurrentAsync()
    {
        var response = await httpClient.GetAsync(UserTimeZone.Append($"{BaseRoute}/current"));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionState>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<GigSessionState> StartAsync(decimal? startOdometer = null, string? vehicleType = null,
        DateTime? startTime = null)
    {
        var response = await httpClient.PostAsJsonAsync(
            UserTimeZone.Append($"{BaseRoute}/start"),
            new StartGigSessionRequest(startTime, startOdometer, vehicleType));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionState>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<GigSessionDto> StopAsync(StopGigSessionRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(UserTimeZone.Append($"{BaseRoute}/stop"), request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionDto>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<IReadOnlyList<GigSessionDto>> ListAsync(int take = 50)
    {
        var response = await httpClient.GetAsync(UserTimeZone.Append($"{BaseRoute}?take={take}"));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<GigSessionDto>>() ?? [];
    }

    public async Task<GigSessionDto> GetAsync(string id)
    {
        var response = await httpClient.GetAsync(UserTimeZone.Append($"{BaseRoute}/{Uri.EscapeDataString(id)}"));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionDto>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<GigSessionDto> CreateAsync(GigSessionRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(UserTimeZone.Append(BaseRoute), request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionDto>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<GigSessionDto> UpdateAsync(string id, GigSessionRequest request)
    {
        var response = await httpClient.PutAsJsonAsync(
            UserTimeZone.Append($"{BaseRoute}/{Uri.EscapeDataString(id)}"), request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GigSessionDto>()
               ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{Uri.EscapeDataString(id)}");
        await Response.EnsureSuccessAsync(response);
    }
}