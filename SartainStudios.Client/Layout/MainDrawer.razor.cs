using Microsoft.AspNetCore.Components;
using SartainStudios.Client.Service;

namespace SartainStudios.Client.Layout;

public partial class MainDrawer(BuildInfoService buildInfoService)
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    private string? BuildVersion { get; set; }
    private string? BuildCommitMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var buildInfo = await buildInfoService.GetAsync();
        if (buildInfo is not null)
        {
            BuildVersion = buildInfo.BuildDateUtc.ToString("yyyy-MM-dd HH:mm");
            BuildCommitMessage = buildInfo.CommitMessage;
        }
    }
}