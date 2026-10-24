using Microsoft.JSInterop;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Component;

public static class VehicleMemory
{
    private const string StorageKey = "sartainstudios.gig.vehicle";

    public static async Task<string?> LoadAsync(IJSRuntime jsRuntime)
    {
        try
        {
            var stored = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(stored) && VehicleTypes.TryNormalize(stored, out var normalized))
                return normalized;
        }
        catch
        {
            // Browser storage unavailable; the caller falls back to its default.
        }

        return null;
    }

    public static async Task SaveAsync(IJSRuntime jsRuntime, string vehicle)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, vehicle);
        }
        catch
        {
            // Not being able to remember the choice should not block the user.
        }
    }
}