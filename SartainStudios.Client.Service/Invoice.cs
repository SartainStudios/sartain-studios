using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Invoice;

namespace SartainStudios.Client.Service;

public sealed class Invoice(HttpClient httpClient)
{
    private const string BasePath = "api/invoices";

    public async Task<IReadOnlyList<Summary>> ListAsync(string? clientId = null, string? status = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(clientId))
            query.Add($"clientId={Uri.EscapeDataString(clientId)}");
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, Lifecycle.AnyStatus, StringComparison.OrdinalIgnoreCase))
            query.Add($"status={Uri.EscapeDataString(status)}");
        var url = query.Count > 0 ? $"{BasePath}?{string.Join("&", query)}" : BasePath;
        return await GetAsync<List<Summary>>(url) ?? [];
    }

    public Task<Detail> GetAsync(string id)
    {
        return ReadAsync<Detail>($"{BasePath}/{id}");
    }

    public async Task<IReadOnlyList<SelectableSession>> GetSelectableSessionsAsync(string contractId)
    {
        var url = $"{BasePath}/selectable-sessions?contractId={Uri.EscapeDataString(contractId)}";
        return await GetAsync<List<SelectableSession>>(url) ?? [];
    }

    public async Task<IReadOnlyList<SelectableSession>> GetEditableSessionsAsync(string invoiceId)
    {
        return await GetAsync<List<SelectableSession>>($"{BasePath}/{invoiceId}/editable-sessions") ?? [];
    }

    public async Task<Detail> GenerateAsync(CreateRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, request);
        return await ReadAsync<Detail>(response);
    }

    public async Task<Detail> EditAsync(string id, EditRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"{BasePath}/{id}", request);
        return await ReadAsync<Detail>(response);
    }

    public async Task<Detail> UpdateStatusAsync(string id, UpdateRequest request)
    {
        var response = await httpClient.PatchAsJsonAsync($"{BasePath}/{id}/status", request);
        return await ReadAsync<Detail>(response);
    }

    public async Task<Detail> SendAsync(string id)
    {
        var response = await httpClient.PostAsync($"{BasePath}/{id}/send", null);
        return await ReadAsync<Detail>(response);
    }

    public async Task<byte[]> DownloadPdfAsync(string id)
    {
        var response = await httpClient.GetAsync($"{BasePath}/{id}/pdf");
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"{BasePath}/{id}");
        await Response.EnsureSuccessAsync(response);
    }

    private async Task<TValue?> GetAsync<TValue>(string url)
    {
        var response = await httpClient.GetAsync(url);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TValue>();
    }

    private async Task<TValue> ReadAsync<TValue>(string url)
    {
        var response = await httpClient.GetAsync(url);
        return await ReadAsync<TValue>(response);
    }

    private static async Task<TValue> ReadAsync<TValue>(HttpResponseMessage response)
    {
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TValue>()
               ?? throw new InvalidOperationException("Empty invoice response.");
    }
}