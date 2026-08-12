using QuestPDF.Fluent;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;

namespace SartainStudios.Api.Service.Invoice;

public static class Document
{
    public const string ContentType = "application/pdf";

    public static byte[] Render(Detail detail, IReadOnlyList<WorkSession> sessions)
    {
        return new PDF.Invoice(detail, sessions).GeneratePdf();
    }

    public static string FileName(string invoiceNumber)
    {
        return $"{invoiceNumber}.pdf";
    }
}