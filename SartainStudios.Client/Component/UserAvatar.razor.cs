using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SartainStudios.Client.Component;

public partial class UserAvatar
{
    private bool _imageFailed;
    private string? _lastPhotoUrl;
    
    [Parameter] public string? PhotoUrl { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    protected override void OnParametersSet()
    {
        if (_lastPhotoUrl == PhotoUrl) return;
        _lastPhotoUrl = PhotoUrl;
        _imageFailed = false;
    }

    private void OnImageError()
    {
        _imageFailed = true;
    }
}