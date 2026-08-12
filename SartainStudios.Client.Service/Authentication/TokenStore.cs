using System.Text.Json;
using Microsoft.JSInterop;
using MudBlazor;
using SartainStudios.Client.Schema;

namespace SartainStudios.Client.Service.Authentication;

public sealed class TokenStore(
    IJSRuntime jsRuntime,
    ISnackbar snackbar)
{
    private const string StorageKey = "sartainstudios.authentication.session";

    public async Task<StoredSession?> LoadAsync()
    {
        var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<StoredSession>(json);
        }
        catch
        {
            snackbar.Add("Failed to load authentication session.", Severity.Error);
            return null;
        }
    }

    public async Task SaveAsync(StoredSession session)
    {
        var json = JsonSerializer.Serialize(session);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task ClearAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}