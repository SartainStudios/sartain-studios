using System.Net.Http.Json;
using SartainStudios.Client.Schema;

namespace SartainStudios.Client.Service;

public sealed class BuildInfoService(HttpClient httpClient)
{
    private BuildInfo? _cached;

    public async Task<BuildInfo?> GetAsync()
    {
        if (_cached is not null) return _cached;
        try
        {
            Console.WriteLine("Fetching build-info.json from server...");
            _cached = await httpClient.GetFromJsonAsync<BuildInfo>("build-info.json");
        }
        catch
        {
            _cached = null;
        }

        return _cached;
    }
}