using System.Reflection;
using MongoDB.Bson;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SartainStudios.Schema;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;
using ClientSnapshot = SartainStudios.Schema.Client.Snapshot;
using OrganizationSnapshot = SartainStudios.Schema.Organization.Snapshot;
using PdfInvoice = SartainStudios.Api.Service.PDF.Invoice;
using ProjectSnapshot = SartainStudios.Schema.Project.Snapshot;

namespace SartainStudios.Api.Service.Test.PDF;

public sealed class InvoiceTests
{
    static InvoiceTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GetMetadata_ReturnsInvoiceTitleAndAuthor()
    {
        var detail = CreateDetail("INV-1001", "Sartain Studios");
        var document = new PdfInvoice(detail, []);

        var metadata = document.GetMetadata();

        Assert.Equal("INV-1001", metadata.Title);
        Assert.Equal("Sartain Studios", metadata.Author);
    }

    [Fact]
    public void GeneratePdf_ReturnsNonEmptyPdfBytes()
    {
        var detail = CreateDetail();
        var sessions = new[]
        {
            CreateSession(
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc))
        };
        var document = new PdfInvoice(detail, sessions);

        var bytes = document.GeneratePdf();

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public void BuildDailyRows_ReturnsContinuousDaysWithZeroGapValues()
    {
        var detail = CreateDetail();
        var sessions = new[]
        {
            CreateSession(
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)),
            CreateSession(
                new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 3, 16, 0, 0, DateTimeKind.Utc))
        };
        var document = new PdfInvoice(detail, sessions);
        var method = typeof(PdfInvoice).GetMethod("BuildDailyRows", BindingFlags.Instance | BindingFlags.NonPublic);

        var rows = Assert.IsAssignableFrom<IReadOnlyList<(DateOnly Date, int Minutes)>>(method!.Invoke(document, null));

        Assert.Equal(3, rows.Count);
        Assert.Equal((new DateOnly(2026, 8, 1), 60), rows[0]);
        Assert.Equal((new DateOnly(2026, 8, 2), 0), rows[1]);
        Assert.Equal((new DateOnly(2026, 8, 3), 120), rows[2]);
    }

    private static Detail CreateDetail(string invoiceNumber = "INV-42", string organizationName = "Acme Org")
    {
        return new Detail(
            "invoice-id",
            "org-id",
            "client-id",
            invoiceNumber,
            new OrganizationSnapshot
            {
                Name = organizationName,
                Address = new Address
                {
                    Line1 = "123 Main St",
                    City = "Nashville",
                    StateOrProvince = "TN",
                    PostalCode = "37201",
                    Country = "USA"
                },
                Email = "billing@acme.test",
                PhoneNumber = "555-0100"
            },
            new ClientSnapshot
            {
                CompanyName = "Client Co",
                ContactPerson = "Client Person",
                Address = new Address
                {
                    Line1 = "500 Broadway",
                    City = "Nashville",
                    StateOrProvince = "TN",
                    PostalCode = "37203",
                    Country = "USA"
                },
                Email = "ap@client.test",
                PhoneNumber = "555-0111"
            },
            new ProjectSnapshot
            {
                ProjectName = "Website Refresh",
                ProjectDescription = "Redesign and implementation",
                ServiceProvided = "Design and Development",
                HourlyRate = 125m,
                BillingCycle = "Monthly",
                ContractId = "contract-1",
                ProjectId = "project-1"
            },
            new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            250m,
            120,
            2,
            125m,
            "Draft",
            [],
            [],
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static WorkSession CreateSession(DateTime startTime, DateTime endTime)
    {
        return new WorkSession
        {
            Id = ObjectId.GenerateNewId(),
            OrganizationId = ObjectId.GenerateNewId(),
            UserId = ObjectId.GenerateNewId(),
            ContractId = ObjectId.GenerateNewId(),
            ProjectId = ObjectId.GenerateNewId(),
            StartTime = startTime,
            EndTime = endTime
        };
    }
}