namespace SartainStudios.Api.Schema.AppSettings;

public sealed class HourLimitMonitor
{
    public const string SectionName = nameof(HourLimitMonitor);
    public int PollIntervalSeconds { get; init; } = 300;
}