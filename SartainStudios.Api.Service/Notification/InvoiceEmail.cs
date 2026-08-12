using System.Globalization;
using System.Net;
using System.Text;
using SartainStudios.Api.Schema.Notification;
using SartainStudios.Schema.Invoice;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;

namespace SartainStudios.Api.Service.Notification;

public static class InvoiceEmail
{
    private const string BorderColor = "#e2e8f0";
    private const string HeaderBackground = "#0f172a";
    private const string MutedColor = "#64748b";
    private const string TextColor = "#0f172a";

    public static EmailRequest Build(InvoiceEntity invoice, Detail detail, byte[] pdf)
    {
        var subject = $"Invoice {invoice.InvoiceNumber} from {invoice.OrganizationSnapshot.Name}";
        var attachment = new EmailAttachment($"{invoice.InvoiceNumber}.pdf", pdf, "application/pdf");
        var replyTo = string.IsNullOrWhiteSpace(invoice.OrganizationSnapshot.Email)
            ? invoice.ClientSnapshot.Email
            : invoice.OrganizationSnapshot.Email;
        var recipients = new[] { invoice.ClientSnapshot.Email };
        var ccRecipients = !string.IsNullOrWhiteSpace(invoice.OrganizationSnapshot.Email)
            ? new[] { invoice.OrganizationSnapshot.Email }
            : [];
        return new EmailRequest(
            recipients,
            ccRecipients,
            replyTo,
            subject,
            BuildTextBody(invoice, detail),
            attachment,
            BuildHtmlBody(invoice, detail));
    }

    private static string BuildTextBody(InvoiceEntity invoice, Detail detail)
    {
        return $"""
                Hello {invoice.ClientSnapshot.ContactPerson},
                Please find attached invoice {invoice.InvoiceNumber} from {invoice.OrganizationSnapshot.Name} for {invoice.TotalAmount:C}, due {invoice.DueDate:d}.
                Summary of days worked:
                {BuildDailyBreakdownTable(detail.DailyBreakdown)}
                Thank you for your business.
                {invoice.OrganizationSnapshot.Name}
                """;
    }

    private static string BuildDailyBreakdownTable(IReadOnlyList<DailyBreakdownEntry> dailyBreakdown)
    {
        if (dailyBreakdown.Count == 0) return "  (no billable days found)";
        const string dateHeader = "Date";
        const string amountHeader = "Amount";
        var rows = dailyBreakdown
            .Select(entry => (Date: entry.Date.ToString("d", CultureInfo.InvariantCulture),
                Amount: $"${entry.Amount.ToString("N2", CultureInfo.InvariantCulture)}"))
            .ToList();
        var dateWidth = Math.Max(dateHeader.Length, rows.Max(row => row.Date.Length));
        var amountWidth = Math.Max(amountHeader.Length, rows.Max(row => row.Amount.Length));
        var topBorder = $"  +{new string('-', dateWidth + 2)}+{new string('-', amountWidth + 2)}+";
        var builder = new StringBuilder();
        builder.AppendLine(topBorder);
        builder.AppendLine($"  | {dateHeader.PadRight(dateWidth)} | {amountHeader.PadLeft(amountWidth)} |");
        builder.AppendLine(topBorder);
        foreach (var row in rows)
            builder.AppendLine($"  | {row.Date.PadRight(dateWidth)} | {row.Amount.PadLeft(amountWidth)} |");
        builder.Append(topBorder);
        return builder.ToString().TrimEnd('\n', '\r');
    }

    private static string BuildHtmlBody(InvoiceEntity invoice, Detail detail)
    {
        var organizationName = Encode(invoice.OrganizationSnapshot.Name);
        var contactPerson = Encode(invoice.ClientSnapshot.ContactPerson);
        var invoiceNumber = Encode(invoice.InvoiceNumber);
        var totalAmount = Encode(invoice.TotalAmount.ToString("C", CultureInfo.CurrentCulture));
        var dueDate = Encode(invoice.DueDate.ToString("D", CultureInfo.CurrentCulture));
        var totalHours = Encode(FormatHours(invoice.TotalMinutesWorked));
        var rows = BuildHtmlRows(detail.DailyBreakdown);
        return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>Invoice {invoiceNumber}</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f1f5f9;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f1f5f9;padding:24px 12px;">
                    <tr>
                      <td align="center">
                        <table role="presentation" width="640" cellpadding="0" cellspacing="0" border="0" style="max-width:640px;width:100%;background-color:#ffffff;border:1px solid {BorderColor};border-radius:8px;overflow:hidden;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:{TextColor};font-size:14px;line-height:1.5;">
                          <tr>
                            <td style="background-color:{HeaderBackground};color:#ffffff;padding:24px;">
                              <div style="font-size:12px;letter-spacing:1px;text-transform:uppercase;opacity:0.75;">Invoice {invoiceNumber}</div>
                              <div style="font-size:22px;font-weight:600;margin-top:4px;">{organizationName}</div>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:24px;">
                              <p style="margin:0 0 16px 0;">Hello {contactPerson},</p>
                              <p style="margin:0 0 20px 0;">Please find attached invoice <strong>{invoiceNumber}</strong> from {organizationName}. A summary of the days worked is below.</p>
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px 0;">
                                <tr>
                                  <td style="padding:12px 16px;background-color:#f8fafc;border:1px solid {BorderColor};border-radius:6px;">
                                    <span style="color:{MutedColor};">Amount due</span>
                                    <div style="font-size:20px;font-weight:600;">{totalAmount}</div>
                                  </td>
                                  <td width="12"></td>
                                  <td style="padding:12px 16px;background-color:#f8fafc;border:1px solid {BorderColor};border-radius:6px;">
                                    <span style="color:{MutedColor};">Due date</span>
                                    <div style="font-size:20px;font-weight:600;">{dueDate}</div>
                                  </td>
                                </tr>
                              </table>
                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border:1px solid {BorderColor};border-radius:6px;border-collapse:separate;overflow:hidden;">
                                <thead>
                                  <tr style="background-color:#f1f5f9;">
                                    <th align="left" style="padding:10px 16px;font-size:12px;letter-spacing:0.5px;text-transform:uppercase;color:{MutedColor};">Date</th>
                                    <th align="right" style="padding:10px 16px;font-size:12px;letter-spacing:0.5px;text-transform:uppercase;color:{MutedColor};">Hours</th>
                                    <th align="right" style="padding:10px 16px;font-size:12px;letter-spacing:0.5px;text-transform:uppercase;color:{MutedColor};">Amount</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {rows}
                                </tbody>
                                <tfoot>
                                  <tr style="background-color:#f1f5f9;">
                                    <td style="padding:12px 16px;border-top:2px solid {BorderColor};font-weight:600;">Total</td>
                                    <td style="padding:12px 16px;border-top:2px solid {BorderColor};font-weight:600;text-align:right;white-space:nowrap;">{totalHours}</td>
                                    <td style="padding:12px 16px;border-top:2px solid {BorderColor};font-weight:600;text-align:right;white-space:nowrap;">{totalAmount}</td>
                                  </tr>
                                </tfoot>
                              </table>
                              <p style="margin:24px 0 0 0;">Thank you for your business.</p>
                              <p style="margin:4px 0 0 0;font-weight:600;">{organizationName}</p>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:16px 24px;border-top:1px solid {BorderColor};color:{MutedColor};font-size:12px;">
                              The full invoice is attached as a PDF.
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
    }

    private static string BuildHtmlRows(IReadOnlyList<DailyBreakdownEntry> dailyBreakdown)
    {
        if (dailyBreakdown.Count == 0)
            return $"""
                    <tr>
                      <td colspan="3" style="padding:12px 16px;border-top:1px solid {BorderColor};color:{MutedColor};font-style:italic;">No billable days found.</td>
                    </tr>
                    """;
        var rows = new StringBuilder();
        var isAlternate = false;
        foreach (var entry in dailyBreakdown)
        {
            var background = isAlternate ? "#f8fafc" : "#ffffff";
            isAlternate = !isAlternate;
            var date = Encode(entry.Date.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture));
            var hours = Encode(FormatHours(entry.MinutesWorked));
            var amount = Encode(entry.Amount.ToString("C", CultureInfo.CurrentCulture));
            rows.Append(
                $"""
                 <tr style="background-color:{background};">
                   <td style="padding:10px 16px;border-top:1px solid {BorderColor};">{date}</td>
                   <td style="padding:10px 16px;border-top:1px solid {BorderColor};text-align:right;white-space:nowrap;">{hours}</td>
                   <td style="padding:10px 16px;border-top:1px solid {BorderColor};text-align:right;white-space:nowrap;">{amount}</td>
                 </tr>
                 """);
        }

        return rows.ToString();
    }

    private static string FormatHours(int minutesWorked)
    {
        var hours = Math.Round(minutesWorked / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours.ToString("F2", CultureInfo.InvariantCulture)}h";
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}