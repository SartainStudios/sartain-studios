using SartainStudios.Api.Service.Invoice;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class DocumentTests
{
    [Fact]
    public void FileName_AppendsPdfExtension()
    {
        var result = Document.FileName("INV42");

        Assert.Equal("INV42.pdf", result);
    }

    [Fact]
    public void ContentType_IsApplicationPdf()
    {
        Assert.Equal("application/pdf", Document.ContentType);
    }
}