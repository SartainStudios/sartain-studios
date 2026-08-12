namespace SartainStudios.Client.Layout;

public class PageTitleState
{
    public const string DefaultTitle = "Sartain Studios LLC";
    public string Title { get; private set; } = DefaultTitle;
    public event Action? Changed;

    public void Set(string? title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
        if (value == Title) return;
        Title = value;
        Changed?.Invoke();
    }

    public void Reset(string previousTitle)
    {
        if (Title == previousTitle) Set(DefaultTitle);
    }
}