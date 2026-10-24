using Microsoft.AspNetCore.Components;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Component;

public sealed partial class WeekSummaryCard
{
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public WeekSummary? Week { get; set; }
    [Parameter] public bool IsLoading { get; set; }

    [Parameter] public bool ShowDeduction { get; set; }
}