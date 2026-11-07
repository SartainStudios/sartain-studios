using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Component;

public sealed partial class GigEarningsImport : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private GigSession GigSession { get; set; } = null!;
    [Parameter] public IReadOnlyList<Source> Sources { get; set; } = [];
    [Parameter] public EventCallback OnSaved { get; set; }

    private ElementReference FileInput;
    private IJSObjectReference? _module;
    private string Platform { get; set; } = EarningsScreenshotSource.Uber;
    private string SourceId { get; set; } = string.Empty;
    private Source? SelectedSource => EarningsScreenshotSource.Find(Platform, Sources);
    private DateTime? Date { get; set; }
    private bool Busy { get; set; }
    private bool Reviewing { get; set; }
    private string? Error { get; set; }
    private string? Notice { get; set; }
    private List<OcrResult> ReadResults { get; set; } = [];
    private List<Row> Rows { get; set; } = [];
    private List<Proposal> Proposals { get; set; } = [];
    private IReadOnlyList<GigSessionDto> Sessions { get; set; } = [];
    private IEnumerable<GigSessionDto> ClosedSessions => Sessions.Where(x => !x.IsOpen && x.EndTime.HasValue);

    private async Task ReadAsync()
    {
        Error = Notice = null;
        if (Date is null)
        {
            Error = "Choose the screenshot date.";
            return;
        }
        if (SelectedSource is not { } source)
        {
            Error = "The matching built-in earnings source is hidden or unavailable. Unhide it under Sources and categories.";
            return;
        }
        SourceId = source.Id;
        Busy = true;
        try
        {
            var zone = TimeZoneInfo.Local;
            var from = DateTime.SpecifyKind(Date.Value.Date, DateTimeKind.Unspecified);
            var to = from.AddDays(2);
            if (zone.IsInvalidTime(from) || zone.IsAmbiguousTime(from) ||
                zone.IsInvalidTime(to) || zone.IsAmbiguousTime(to))
                throw new InvalidOperationException("The selected date has an ambiguous time zone boundary.");
            Sessions = await GigSession.ListOverlappingAsync(
                TimeZoneInfo.ConvertTimeToUtc(from, zone), TimeZoneInfo.ConvertTimeToUtc(to, zone));
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "./js/earnings-ocr.js");
            await ReleaseScreenshotsAsync();
            ReadResults = await _module.InvokeAsync<List<OcrResult>>("readScreenshots", CancellationToken.None, FileInput);
            foreach (var result in ReadResults)
            {
                var readings = Platform == EarningsScreenshotSource.Uber
                    ? EarningsScreenshotParser.ReadUber(result.Text)
                    : EarningsScreenshotParser.ReadDoorDash(result.Text);
                foreach (var reading in readings)
                {
                    var row = new Row(result.Name, reading.Amount, reading.Start, reading.End);
                    Suggest(row);
                    Rows.Add(row);
                }
                if (readings.Count == 0)
                    Notice = "At least one screenshot produced no recognizable rows. Check the recognized text and add missing rows manually.";
            }
            if (Rows.Count == 0)
                AddRow();
        }
        catch (Exception ex)
        {
            Error = $"Could not read the screenshots: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    private void Suggest(Row row)
    {
        row.SessionId = string.Empty;
        if (Date is null || row.Start is null || (Platform == EarningsScreenshotSource.DoorDash && row.End is null))
            return;
        row.SessionId = EarningsSessionMatcher.Suggest(Date.Value, row.Start.Value,
            Platform == EarningsScreenshotSource.DoorDash ? row.End : null, Sessions, TimeZoneInfo.Local) ?? string.Empty;
    }

    private void ChangeStart(Row row, TimeSpan? value)
    {
        row.Start = value;
        Suggest(row);
    }

    private void ChangeEnd(Row row, TimeSpan? value)
    {
        row.End = value;
        Suggest(row);
    }

    private void AddRow() => Rows.Add(new Row("Manual entry", null, null, null));

    private void Prepare()
    {
        Error = Notice = null;
        if (Rows.Any(row => row.Amount is null or < 0 or > 1000000 || row.Start is null ||
                            (Platform == EarningsScreenshotSource.DoorDash && row.End is null)))
        {
            Error = "Enter a valid amount and time for every row, including rows you leave unassigned.";
            return;
        }
        var groups = Rows.Where(row => !string.IsNullOrEmpty(row.SessionId)).GroupBy(row => row.SessionId);
        Proposals = [];
        foreach (var group in groups)
        {
            var session = ClosedSessions.FirstOrDefault(x => x.Id == group.Key);
            if (session is null)
            {
                Error = "A selected session is no longer available. Reload and review your entries.";
                return;
            }
            Proposals.Add(new Proposal(session, group.Sum(x => x.Amount!.Value), group.Count(),
                session.Earnings.Where(x => x.SourceId == SourceId).Sum(x => x.Amount)));
        }
        if (Proposals.Count == 0)
        {
            Error = "Assign at least one row to a session before reviewing totals.";
            return;
        }
        Reviewing = true;
    }

    private async Task SaveAsync()
    {
        Error = Notice = null;
        if (SelectedSource?.Id != SourceId)
        {
            Error = "The matching earnings source is no longer available. Start over after unhiding it.";
            return;
        }
        var selected = Proposals.Where(p => !p.Saved && p.Action != "Skip").ToList();
        if (selected.Count == 0)
        {
            Error = "Choose Add or Replace for at least one session.";
            return;
        }
        Busy = true;
        try
        {
            foreach (var proposal in selected)
            {
                var current = await GigSession.GetAsync(proposal.Session.Id);
                if (!Unchanged(proposal.Session, current))
                    throw new InvalidOperationException("A session changed since review. Start over and review the current values before saving.");
                var earnings = proposal.Action == "Replace"
                    ? current.Earnings.Where(x => x.SourceId != SourceId).ToList()
                    : current.Earnings.ToList();
                earnings.Add(new EarningEntry(SourceId, proposal.Amount, null));
                await GigSession.UpdateAsync(current.Id, new GigSessionRequest(current.StartTime, current.EndTime,
                    current.StartOdometer, current.EndOdometer, current.VehicleType, current.Notes, earnings, current.Expenses));
                proposal.Saved = true;
            }
            Notice = "Selected session totals were saved. Review any skipped or unassigned rows separately.";
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Error = $"Saving stopped: {ex.Message} Sessions marked Saved were already updated; do not import them again.";
            await OnSaved.InvokeAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    private static bool Unchanged(GigSessionDto a, GigSessionDto b) =>
        a.StartTime == b.StartTime && a.EndTime == b.EndTime && a.StartOdometer == b.StartOdometer &&
        a.EndOdometer == b.EndOdometer && a.VehicleType == b.VehicleType && a.Notes == b.Notes &&
        a.Earnings.SequenceEqual(b.Earnings) && a.Expenses.SequenceEqual(b.Expenses);

    private void BackToRows()
    {
        if (Proposals.Any(x => x.Saved))
        {
            Error = "Some totals have already been saved. Start over to avoid saving them twice.";
            return;
        }
        Reviewing = false;
        Error = Notice = null;
    }

    private async Task Reset()
    {
        await ReleaseScreenshotsAsync();
        Rows = [];
        Proposals = [];
        ReadResults = [];
        Sessions = [];
        SourceId = string.Empty;
        Reviewing = false;
        Error = Notice = null;
    }

    private static string SessionLabel(GigSessionDto session) =>
        $"{session.StartTime.ToLocalTime():g} – {session.EndTime!.Value.ToLocalTime():g}";

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await ReleaseScreenshotsAsync();
            await _module.DisposeAsync();
        }
    }

    private async Task ReleaseScreenshotsAsync()
    {
        if (_module is not null && ReadResults.Count > 0)
            await _module.InvokeVoidAsync("releaseScreenshots", ReadResults.Select(x => x.Url).ToArray());
        ReadResults = [];
    }

    private sealed record OcrResult(string Name, string Text, string Url);

    private sealed class Row(string fileName, decimal? amount, TimeSpan? start, TimeSpan? end)
    {
        public string FileName { get; } = fileName;
        public decimal? Amount { get; set; } = amount;
        public TimeSpan? Start { get; set; } = start;
        public TimeSpan? End { get; set; } = end;
        public string SessionId { get; set; } = string.Empty;
    }

    private sealed class Proposal(GigSessionDto session, decimal amount, int count, decimal existing)
    {
        public GigSessionDto Session { get; } = session;
        public decimal Amount { get; } = amount;
        public int Count { get; } = count;
        public decimal Existing { get; } = existing;
        public string Action { get; set; } = "Skip";
        public bool Saved { get; set; }
    }
}