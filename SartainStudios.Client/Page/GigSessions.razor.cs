using MudBlazor;
using SartainStudios.Client.Component;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Page;

public sealed partial class GigSessions(
    GigSession gigSession,
    GigSource gigSource,
    GigSettings gigSettings,
    ISnackbar snackbar)
{
    private List<GigSessionDto> Items { get; set; } = [];
    private IReadOnlyList<Source> Sources { get; set; } = [];
    private IReadOnlyList<Category> Categories { get; set; } = [];
    private bool MileageDeductionEnabled { get; set; }
    private GigSessionDto? Editing { get; set; }
    private GigSessionDto? Deleting { get; set; }
    private GigSessionDialogMode DialogMode { get; set; } = GigSessionDialogMode.Edit;
    private bool DialogVisible { get; set; }
    private bool DeleteVisible { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsBusy { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var sessions = gigSession.ListAsync(200);
            var sources = gigSource.ListSourcesAsync();
            var categories = gigSource.ListCategoriesAsync();
            var settings = gigSettings.GetAsync();
            Items = (await sessions).ToList();
            Sources = await sources;
            Categories = await categories;
            MileageDeductionEnabled = (await settings).MileageDeductionEnabled;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            snackbar.Add(ex.Message, Severity.Error);
            Items = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenCreate()
    {
        Editing = null;
        DialogMode = GigSessionDialogMode.Create;
        DialogVisible = true;
    }

    private void OpenEdit(GigSessionDto session)
    {
        Editing = session;
        DialogMode = GigSessionDialogMode.Edit;
        DialogVisible = true;
    }

    private void ConfirmDelete(GigSessionDto session)
    {
        Deleting = session;
        DeleteVisible = true;
    }

    private async Task SaveAsync(GigSessionDialog.GigSessionInput input)
    {
        IsBusy = true;
        try
        {
            var request = new GigSessionRequest(
                input.StartTimeUtc,
                input.EndTimeUtc,
                input.StartOdometer,
                input.EndOdometer,
                input.VehicleType,
                input.Notes,
                input.Earnings,
                input.Expenses);
            if (DialogMode == GigSessionDialogMode.Create)
                await gigSession.CreateAsync(request);
            else
                await gigSession.UpdateAsync(Editing!.Id, request);
            DialogVisible = false;
            snackbar.Add("Session saved.", Severity.Success);
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

    private async Task DeleteAsync()
    {
        if (Deleting is null) return;
        IsBusy = true;
        try
        {
            await gigSession.DeleteAsync(Deleting.Id);
            DeleteVisible = false;
            snackbar.Add("Session deleted.", Severity.Success);
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

    private static string HourlyRate(GigSessionDto session)
    {
        return session.Minutes > 0
            ? $"{GigFormat.Money(session.Profit / (session.Minutes / 60m))}/hr"
            : "-";
    }
}