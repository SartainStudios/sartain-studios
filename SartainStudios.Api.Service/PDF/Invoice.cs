using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;

namespace SartainStudios.Api.Service.PDF;

public sealed class Invoice(Detail detail, IReadOnlyList<WorkSession> sessions) : IDocument
{
    public DocumentMetadata GetMetadata()
    {
        return new DocumentMetadata
        {
            Title = detail.InvoiceNumber,
            Author = detail.OrganizationSnapshot.Name
        };
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
            page.Header().Element(Header);
            page.Content().PaddingTop(16).Element(Content);
            page.Footer().Element(Footer);
        });
    }

    private void Header(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("INVOICE")
                    .FontSize(28).Bold().FontColor(Colors.Grey.Darken3);
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text($"Invoice #: {detail.InvoiceNumber}").Bold();
                    c.Item().Text($"Date: {detail.CreatedAt:MM/dd/yyyy}");
                    c.Item().Text($"Due: {detail.DueDate:MM/dd/yyyy}");
                });
            });
            column.Item().PaddingTop(8).LineHorizontal(2).LineColor(Colors.Grey.Darken3);
        });
    }

    private void Content(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(16);
            column.Item().Element(OrgAndClientRow);
            column.Item().Element(ProjectInfoSection);
            column.Item().Element(SummarySection);
            column.Item().Element(BillingTable);
        });
    }

    private void OrgAndClientRow(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(OrgSection);
            row.ConstantItem(40);
            row.RelativeItem().Element(ClientSection);
        });
    }

    private void OrgSection(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("FROM").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).Text(detail.OrganizationSnapshot.Name).Bold();
            foreach (var line in detail.OrganizationSnapshot.Address.ToLines())
                column.Item().Text(line);
            column.Item().Text(detail.OrganizationSnapshot.Email);
            column.Item().Text(detail.OrganizationSnapshot.PhoneNumber);
        });
    }

    private void ClientSection(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("BILL TO").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).Text(detail.ClientSnapshot.CompanyName).Bold();
            column.Item().Text(detail.ClientSnapshot.ContactPerson);
            foreach (var line in detail.ClientSnapshot.Address.ToLines())
                column.Item().Text(line);
            column.Item().Text(detail.ClientSnapshot.Email);
            column.Item().Text(detail.ClientSnapshot.PhoneNumber);
        });
    }

    private void ProjectInfoSection(IContainer container)
    {
        container.Background(Colors.Grey.Lighten3).Padding(12).Column(column =>
        {
            column.Item().Text("PROJECT INFORMATION").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Project Name").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(detail.ProjectSnapshot.ProjectName);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Service Provided").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(detail.ProjectSnapshot.ServiceProvided);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Hourly Rate").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text($"${detail.ProjectSnapshot.HourlyRate:N2}/hr");
                });
            });
        });
    }

    private void SummarySection(IContainer container)
    {
        var totalHours = detail.TotalMinutesWorked / 60m;
        container.Row(row =>
        {
            row.RelativeItem().Element(c => SummaryCard(c, "Total Days Worked", detail.TotalDaysWorked.ToString()));
            row.ConstantItem(8);
            row.RelativeItem().Element(c => SummaryCard(c, "Total Hours Worked", $"{totalHours:F2}"));
            row.ConstantItem(8);
            row.RelativeItem().Element(c => SummaryCard(c, "Avg Cost / Day", $"${detail.AverageRevenuePerDay:N2}"));
        });
    }

    private static void SummaryCard(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(2).Text(value).FontSize(14).Bold();
        });
    }

    private void BillingTable(IContainer container)
    {
        var dailyRows = BuildDailyRows();
        var hourlyRate = detail.ProjectSnapshot.HourlyRate;
        var serviceProvided = detail.ProjectSnapshot.ServiceProvided;
        container.Column(column =>
        {
            column.Item().Text("TIME & BILLING BREAKDOWN").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(80);
                    cols.ConstantColumn(85);
                    cols.RelativeColumn();
                    cols.ConstantColumn(55);
                    cols.ConstantColumn(65);
                    cols.ConstantColumn(75);
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Day of Week");
                    header.Cell().Element(HeaderCell).Text("Date");
                    header.Cell().Element(HeaderCell).Text("Description");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Hours");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                });
                foreach (var (date, minutes) in dailyRows)
                {
                    var hours = minutes / 60m;
                    var rowTotal = Math.Round(hourlyRate * minutes / 60m, 2, MidpointRounding.AwayFromZero);
                    table.Cell().Element(DataCell).Text(date.DayOfWeek.ToString());
                    table.Cell().Element(DataCell).Text(date.ToString("MM/dd/yyyy"));
                    table.Cell().Element(DataCell).Text(minutes > 0 ? serviceProvided : "-");
                    table.Cell().Element(DataCell).AlignRight().Text(minutes > 0 ? $"{hours:F2}" : "0");
                    table.Cell().Element(DataCell).AlignRight().Text($"${hourlyRate:N2}");
                    table.Cell().Element(DataCell).AlignRight().Text(minutes > 0 ? $"${rowTotal:N2}" : "$0.00");
                }

                table.Cell().ColumnSpan(5).Element(GrandTotalCell).AlignRight()
                    .Text("Grand Total").Bold().FontSize(11);
                table.Cell().Element(GrandTotalCell).AlignRight()
                    .Text($"${detail.TotalAmount:N2}").Bold().FontSize(11);
            });
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background(Colors.Grey.Darken3).Padding(6)
            .DefaultTextStyle(x => x.Bold().FontColor(Colors.White).FontSize(9));
    }

    private static IContainer DataCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
    }

    private static IContainer GrandTotalCell(IContainer container)
    {
        return container.Background(Colors.Grey.Lighten3).Padding(8);
    }

    private void Footer(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(8).AlignCenter().Text(text =>
            {
                text.Span("Thank you for your business").Bold();
                text.Span("   •   ").FontColor(Colors.Grey.Darken1);
                text.Span(detail.OrganizationSnapshot.Name);
                text.Span("   •   ").FontColor(Colors.Grey.Darken1);
                text.Span(detail.OrganizationSnapshot.Email).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private IReadOnlyList<(DateOnly Date, int Minutes)> BuildDailyRows()
    {
        if (sessions.Count == 0) return [];
        var completedSessions = sessions.Where(s => s.EndTime.HasValue).ToList();
        if (completedSessions.Count == 0) return [];
        var dailyMinutes = new Dictionary<DateOnly, int>();
        foreach (var session in completedSessions)
        {
            var startDate = DateOnly.FromDateTime(session.StartTime);
            var endDate = DateOnly.FromDateTime(session.EndTime!.Value);
            var current = startDate;
            while (current <= endDate)
            {
                var dayStart = current.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEnd = current.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var clippedStart = session.StartTime < dayStart ? dayStart : session.StartTime;
                var clippedEnd = session.EndTime!.Value > dayEnd ? dayEnd : session.EndTime!.Value;
                if (clippedEnd > clippedStart)
                {
                    var minutes = (int)Math.Floor((clippedEnd - clippedStart).TotalMinutes);
                    dailyMinutes[current] = dailyMinutes.GetValueOrDefault(current, 0) + minutes;
                }

                current = current.AddDays(1);
            }
        }

        var firstDate = DateOnly.FromDateTime(completedSessions.Min(s => s.StartTime));
        var lastDate = DateOnly.FromDateTime(completedSessions.Max(s => s.EndTime!.Value));
        var rows = new List<(DateOnly, int)>();
        var day = firstDate;
        while (day <= lastDate)
        {
            rows.Add((day, dailyMinutes.GetValueOrDefault(day, 0)));
            day = day.AddDays(1);
        }

        return rows;
    }
}