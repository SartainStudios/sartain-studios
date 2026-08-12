using SartainStudios.Schema.Billing;

namespace SartainStudios.Client.Schema;

public sealed record ContractProgressItem(Summary Contract, int BaseLoggedMinutes, int RunningMinutes)
{
    public int LoggedMinutes => BaseLoggedMinutes + RunningMinutes;
    public int RemainingMinutes => Contract.ExpectedMinutes - LoggedMinutes;

    public double ProgressPercent => Contract.ExpectedMinutes <= 0
        ? 0
        : Math.Min(100d, LoggedMinutes * 100d / Contract.ExpectedMinutes);

    public string RemainingMessage => RemainingMinutes > 0
        ? $"{RemainingMinutes} minute{(RemainingMinutes == 1 ? string.Empty : "s")} ({FormatHours(RemainingMinutes)}) remaining"
        : RemainingMinutes < 0
            ? $"{Math.Abs(RemainingMinutes)} minute{(Math.Abs(RemainingMinutes) == 1 ? string.Empty : "s")} ({FormatHours(Math.Abs(RemainingMinutes))}) over budget"
            : "At target";

    public string Status => RemainingMinutes > 0
        ? "On track"
        : RemainingMinutes < 0
            ? "Over budget"
            : "Target met";

    private static string FormatHours(int minutes)
    {
        var hours = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours:F2}h";
    }
}