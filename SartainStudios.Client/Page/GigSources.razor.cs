using MudBlazor;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;
using OptionItem = SartainStudios.Client.Component.GigOptionList.OptionItem;

namespace SartainStudios.Client.Page;

public sealed partial class GigSources(GigSource gigSource, ISnackbar snackbar)
{
    private IReadOnlyList<Source> Sources { get; set; } = [];
    private IReadOnlyList<Category> Categories { get; set; } = [];
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsBusy { get; set; }

    private IReadOnlyList<OptionItem> SourceItems =>
        Sources.Select(s => new OptionItem(s.Id, s.Name, s.Color, s.IsDefault, s.IsHidden)).ToList();

    private IReadOnlyList<OptionItem> CategoryItems =>
        Categories.Select(c => new OptionItem(c.Id, c.Name, c.Color, c.IsDefault, c.IsHidden)).ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        IsLoading = false;
    }

    private async Task LoadAsync()
    {
        try
        {
            var sources = gigSource.ListSourcesAsync();
            var categories = gigSource.ListCategoriesAsync();
            Sources = await sources;
            Categories = await categories;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private Task CreateSourceAsync(OptionRequest request)
    {
        return RunAsync(() => gigSource.CreateSourceAsync(request), "Source added.");
    }

    private Task UpdateSourceAsync((string Id, OptionRequest Request) update)
    {
        return RunAsync(() => gigSource.UpdateSourceAsync(update.Id, update.Request), "Source updated.");
    }

    private Task ToggleSourceAsync(OptionItem item)
    {
        return RunAsync(() => gigSource.SetSourceHiddenAsync(item.Id, !item.IsHidden), null);
    }

    private Task DeleteSourceAsync(OptionItem item)
    {
        return RunAsync(() => gigSource.DeleteSourceAsync(item.Id), "Source deleted.");
    }

    private Task CreateCategoryAsync(OptionRequest request)
    {
        return RunAsync(() => gigSource.CreateCategoryAsync(request), "Category added.");
    }

    private Task UpdateCategoryAsync((string Id, OptionRequest Request) update)
    {
        return RunAsync(() => gigSource.UpdateCategoryAsync(update.Id, update.Request), "Category updated.");
    }

    private Task ToggleCategoryAsync(OptionItem item)
    {
        return RunAsync(() => gigSource.SetCategoryHiddenAsync(item.Id, !item.IsHidden), null);
    }

    private Task DeleteCategoryAsync(OptionItem item)
    {
        return RunAsync(() => gigSource.DeleteCategoryAsync(item.Id), "Category deleted.");
    }

    private async Task RunAsync(Func<Task> action, string? successMessage)
    {
        IsBusy = true;
        try
        {
            await action();
            if (successMessage is not null) snackbar.Add(successMessage, Severity.Success);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}