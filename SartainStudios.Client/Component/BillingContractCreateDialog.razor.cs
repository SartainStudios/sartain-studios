using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Service.Validation;
using SartainStudios.Schema.Billing;
using ProjectSummary = SartainStudios.Schema.Project.Summary;

namespace SartainStudios.Client.Component;

public sealed partial class BillingContractCreateDialog
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private bool _wasVisible;
    private MudForm CreateForm { get; set; } = null!;
    private string NewProjectId { get; set; } = string.Empty;
    private string NewServiceProvided { get; set; } = string.Empty;
    private decimal NewHourlyRate { get; set; } = 10;
    private decimal NewExpectedHours { get; set; } = 10000;
    private string NewBillingCycle { get; set; } = nameof(Cycle.Weekly);
    private string NewInvoicePrefix { get; set; } = string.Empty;
    private bool NewIsActive { get; set; } = true;
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public IReadOnlyList<ProjectSummary> Projects { get; set; } = [];
    [Parameter] public IReadOnlyList<string> BillingCycles { get; set; } = [];
    [Parameter] public EventCallback<CreateInput> OnCreate { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
            ResetFields();
        _wasVisible = Visible;
    }

    private void ResetFields()
    {
        NewProjectId = string.Empty;
        NewServiceProvided = string.Empty;
        NewHourlyRate = 10;
        NewExpectedHours = 10000;
        NewBillingCycle = BillingCycles.Count > 0 ? BillingCycles[0] : nameof(Cycle.Weekly);
        NewInvoicePrefix = string.Empty;
        NewIsActive = true;
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        return VisibleChanged.InvokeAsync(visible);
    }

    private static string? ValidateServiceProvided(string? value)
    {
        return FieldValidators.ValidateRequiredText(value, "Service provided");
    }

    private static string? ValidateHourlyRate(decimal value)
    {
        return FieldValidators.ValidatePositiveAmount(value, "Hourly rate");
    }

    private static string? ValidateExpectedHours(decimal value)
    {
        return FieldValidators.ValidatePositiveAmount(value, "Expected hours");
    }

    private Task CloseAsync()
    {
        return VisibleChanged.InvokeAsync(false);
    }

    private async Task CreateAsync()
    {
        await CreateForm.ValidateAsync();
        if (!CreateForm.IsValid || !OnCreate.HasDelegate)
            return;
        await OnCreate.InvokeAsync(new CreateInput(
            NewProjectId,
            NewHourlyRate,
            NewExpectedHours,
            NewBillingCycle,
            NewServiceProvided,
            NewInvoicePrefix,
            NewIsActive));
    }

    public sealed record CreateInput(
        string ProjectId,
        decimal HourlyRate,
        decimal ExpectedHours,
        string BillingCycle,
        string ServiceProvided,
        string InvoicePrefix,
        bool IsActive);
}