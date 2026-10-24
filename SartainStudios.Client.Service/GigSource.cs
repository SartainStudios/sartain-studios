using System.Net.Http.Json;
using SartainStudios.Client.Schema.Api;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Service;

public sealed class GigSource(HttpClient httpClient)
{
    private const string SourcesRoute = "api/gig/sources";
    private const string CategoriesRoute = "api/gig/categories";

    public async Task<IReadOnlyList<Source>> ListSourcesAsync()
    {
        var response = await httpClient.GetAsync(SourcesRoute);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Source>>() ?? [];
    }

    public async Task<Source> CreateSourceAsync(OptionRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(SourcesRoute, request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Source>()
               ?? throw new InvalidOperationException("Empty source response.");
    }

    public async Task<Source> UpdateSourceAsync(string id, OptionRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"{SourcesRoute}/{Uri.EscapeDataString(id)}", request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Source>()
               ?? throw new InvalidOperationException("Empty source response.");
    }

    public async Task<Source> SetSourceHiddenAsync(string id, bool isHidden)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{SourcesRoute}/{Uri.EscapeDataString(id)}/hidden", new HiddenRequest(isHidden));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Source>()
               ?? throw new InvalidOperationException("Empty source response.");
    }

    public async Task DeleteSourceAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"{SourcesRoute}/{Uri.EscapeDataString(id)}");
        await Response.EnsureSuccessAsync(response);
    }

    public async Task<IReadOnlyList<Category>> ListCategoriesAsync()
    {
        var response = await httpClient.GetAsync(CategoriesRoute);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<Category>>() ?? [];
    }

    public async Task<Category> CreateCategoryAsync(OptionRequest request)
    {
        var response = await httpClient.PostAsJsonAsync(CategoriesRoute, request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Category>()
               ?? throw new InvalidOperationException("Empty category response.");
    }

    public async Task<Category> UpdateCategoryAsync(string id, OptionRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"{CategoriesRoute}/{Uri.EscapeDataString(id)}", request);
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Category>()
               ?? throw new InvalidOperationException("Empty category response.");
    }

    public async Task<Category> SetCategoryHiddenAsync(string id, bool isHidden)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{CategoriesRoute}/{Uri.EscapeDataString(id)}/hidden", new HiddenRequest(isHidden));
        await Response.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Category>()
               ?? throw new InvalidOperationException("Empty category response.");
    }

    public async Task DeleteCategoryAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"{CategoriesRoute}/{Uri.EscapeDataString(id)}");
        await Response.EnsureSuccessAsync(response);
    }
}