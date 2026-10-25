using Microsoft.AspNetCore.Components;
using SartainStudios.Client.Service;

namespace SartainStudios.Client.Layout;

public partial class SectionDrawer(BuildInfoService buildInfoService)
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public RenderFragment? NavLinks { get; set; }
    [Parameter] public bool ShowBuildInfo { get; set; }
    private string? BuildVersion { get; set; }
    private string? BuildCommitMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!ShowBuildInfo)
            return;
        var buildInfo = await buildInfoService.GetAsync();
        if (buildInfo is not null)
        {
            BuildVersion = buildInfo.BuildDateUtc.ToString("yyyy-MM-dd HH:mm");
            BuildCommitMessage = buildInfo.CommitMessage;
        }
    }
}