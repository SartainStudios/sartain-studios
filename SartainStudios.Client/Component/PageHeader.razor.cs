using Microsoft.AspNetCore.Components;
using SartainStudios.Client.Layout;

namespace SartainStudios.Client.Component;

public partial class PageHeader(PageTitleState pageTitleState) : ComponentBase, IDisposable
{
    private string? _publishedTitle;
    [Parameter] public string? Title { get; set; }

    public void Dispose()
    {
        if (_publishedTitle is not null) pageTitleState.Reset(_publishedTitle);
    }

    protected override void OnParametersSet()
    {
        _publishedTitle = string.IsNullOrWhiteSpace(Title) ? PageTitleState.DefaultTitle : Title.Trim();
        pageTitleState.Set(Title);
    }
}