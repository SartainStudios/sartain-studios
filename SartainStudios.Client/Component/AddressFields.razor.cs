using Microsoft.AspNetCore.Components;
using SartainStudios.Schema;

namespace SartainStudios.Client.Component;

public sealed partial class AddressFields
{
    [Parameter] public Address Value { get; set; } = new();
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Required { get; set; }
}