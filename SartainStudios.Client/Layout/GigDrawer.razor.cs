using Microsoft.AspNetCore.Components;

namespace SartainStudios.Client.Layout;

public partial class GigDrawer
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
}