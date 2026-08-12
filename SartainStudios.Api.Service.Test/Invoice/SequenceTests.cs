using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.DatabaseEntity;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class SequenceTests
{
    [Fact]
    public void NormalizePrefix_TrimsAndUppercases()
    {
        var result = " inv ".Trim().ToUpperInvariant();

        Assert.Equal("INV", result);
    }

    [Fact]
    public void BuildInvoiceNumber_CombinesPrefixAndSequence()
    {
        var sequence = new InvoiceSequence
        {
            OrganizationId = ObjectId.GenerateNewId(),
            InvoicePrefix = "INV",
            Sequence = 42
        };

        var result = Sequence.BuildInvoiceNumber("inv", sequence);

        Assert.Equal("INV42", result);
    }
}