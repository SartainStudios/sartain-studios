namespace SartainStudios.Api.Schema.Invoice;

public sealed record InvoiceDocument(string FileName, string ContentType, byte[] Content);