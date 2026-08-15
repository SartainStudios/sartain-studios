using MongoDB.Bson;
using SartainStudios.Api.Service.Invoice;
using SartainStudios.Schema.Client;
using SartainStudios.Schema.DatabaseEntity;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;

namespace SartainStudios.Api.Service.Test.Invoice;

public sealed class PresentationTests
{
    private static SartainStudios.Schema.DatabaseEntity.Invoice CreateInvoice()
    {
        return new SartainStudios.Schema.DatabaseEntity.Invoice
        {
            OrganizationId = ObjectId.GenerateNewId(),
            ClientId = ObjectId.GenerateNewId(),
            InvoiceNumber = "INV1",
            DueDate = DateTime.UtcNow,
            ClientSnapshot = new Snapshot { CompanyName = "Acme" },
            ProjectSnapshot = new SartainStudios.Schema.Project.Snapshot { ProjectName = "Website", HourlyRate = 50m },
            Status = "Draft",
            BilledSessionIds = []
        };
    }

    [Fact]
    public void ToSummary_MapsCoreFields()
    {
        var invoice = CreateInvoice();

        var summary = Presentation.ToSummary(invoice);

        Assert.Equal(invoice.InvoiceNumber, summary.InvoiceNumber);
        Assert.Equal("Acme", summary.ClientCompanyName);
        Assert.Equal("Website", summary.ProjectName);
    }

    [Fact]
    public void ToDetail_MapsCoreFields()
    {
        var invoice = CreateInvoice();

        var detail = Presentation.ToDetail(invoice, [], TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));

        Assert.Equal(invoice.InvoiceNumber, detail.InvoiceNumber);
        Assert.Equal(invoice.Status, detail.Status);
        Assert.Empty(detail.DailyBreakdown);
    }

    [Fact]
    public void ToSelectableSession_CalculatesMinutesWorked()
    {
        var project = new SartainStudios.Schema.DatabaseEntity.Project { Name = "Website" };
        var contract = new BillingContract { ServiceProvided = "Consulting" };
        var session = new WorkSession
        {
            StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var result = Presentation.ToSelectableSession(session, project, contract);

        Assert.Equal(60, result.MinutesWorked);
        Assert.Equal("Website", result.ProjectName);
    }

    [Fact]
    public void ToOrganizationSnapshot_MapsFields()
    {
        var organization = new OrganizationEntity { Name = "Sartain Studios", Email = "info@sartain.dev" };

        var snapshot = Presentation.ToOrganizationSnapshot(organization);

        Assert.Equal("Sartain Studios", snapshot.Name);
        Assert.Equal("info@sartain.dev", snapshot.Email);
    }

    [Fact]
    public void ToClientSnapshot_MapsFields()
    {
        var client = new SartainStudios.Schema.DatabaseEntity.Client { CompanyName = "Acme", Email = "hi@acme.dev" };

        var snapshot = Presentation.ToClientSnapshot(client);

        Assert.Equal("Acme", snapshot.CompanyName);
        Assert.Equal("hi@acme.dev", snapshot.Email);
    }

    [Fact]
    public void ToProjectSnapshot_MapsFields()
    {
        var project = new SartainStudios.Schema.DatabaseEntity.Project
            { Name = "Website", Description = "Marketing site" };
        var contract = new BillingContract { ServiceProvided = "Consulting", HourlyRate = 75m };

        var snapshot = Presentation.ToProjectSnapshot(project, contract);

        Assert.Equal("Website", snapshot.ProjectName);
        Assert.Equal(75m, snapshot.HourlyRate);
        Assert.Equal(contract.Id.ToString(), snapshot.ContractId);
    }
}