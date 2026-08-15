using MongoDB.Bson;
using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using Status = SartainStudios.Schema.Invoice.Status;

namespace SartainStudios.Api.Service.Invoice;

public sealed class Draft(Database database)
{
    public static bool IsDraft(InvoiceEntity invoice)
    {
        return Lifecycle.IsDraft(invoice.Status);
    }

    public async Task<InvoiceEntity?> LoadAsync(ObjectId organizationId, ObjectId invoiceId,
        IClientSessionHandle? mongoSession = null)
    {
        var invoice = mongoSession is null
            ? await database.Invoices
                .Find(x => x.Id == invoiceId && x.OrganizationId == organizationId)
                .FirstOrDefaultAsync()
            : await database.Invoices
                .Find(mongoSession, x => x.Id == invoiceId && x.OrganizationId == organizationId)
                .FirstOrDefaultAsync();
        return invoice is not null && IsDraft(invoice) ? invoice : null;
    }

    public async Task<bool> RecalculateOrDeleteAsync(
        IClientSessionHandle mongoSession,
        InvoiceEntity invoice,
        TimeZoneInfo userTimeZone)
    {
        var sessions = await database.TimeSessions
            .Find(mongoSession, x => x.InvoiceId == invoice.Id && x.OrganizationId == invoice.OrganizationId)
            .SortBy(x => x.StartTime)
            .ToListAsync();

        if (sessions.Count == 0)
        {
            await database.Invoices.DeleteOneAsync(mongoSession, x => x.Id == invoice.Id);
            return false;
        }

        var hourlyRate = await ResolveHourlyRateAsync(mongoSession, invoice);
        var totals = Totals.Calculate(sessions, hourlyRate, userTimeZone);

        invoice.ProjectSnapshot.HourlyRate = hourlyRate;
        invoice.TotalAmount = totals.TotalAmount;
        invoice.TotalMinutesWorked = totals.TotalMinutesWorked;
        invoice.TotalDaysWorked = totals.TotalDaysWorked;
        invoice.AverageRevenuePerDay = totals.AverageRevenuePerDay;
        invoice.BilledSessionIds = sessions.Select(x => x.Id).ToArray();
        invoice.UpdatedAt = DateTime.UtcNow;

        await database.Invoices.ReplaceOneAsync(mongoSession, x => x.Id == invoice.Id, invoice);
        return true;
    }

    public async Task RefreshForContractAsync(
        ObjectId organizationId,
        BillingContract contract,
        SartainStudios.Schema.DatabaseEntity.Project project,
        TimeZoneInfo userTimeZone)
    {
        var contractId = contract.Id.ToString();
        var draftInvoices = await database.Invoices
            .Find(x => x.OrganizationId == organizationId && x.Status == nameof(Status.Draft) &&
                       x.ProjectSnapshot.ContractId == contractId)
            .ToListAsync();

        if (draftInvoices.Count == 0) return;
        var now = DateTime.UtcNow;

        foreach (var invoice in draftInvoices)
        {
            var sessions = await database.TimeSessions
                .Find(x => x.InvoiceId == invoice.Id && x.OrganizationId == organizationId)
                .SortBy(x => x.StartTime)
                .ToListAsync();

            var totals = Totals.Calculate(sessions, contract.HourlyRate, userTimeZone);

            invoice.ProjectSnapshot.ProjectName = project.Name;
            invoice.ProjectSnapshot.ProjectDescription = project.Description;
            invoice.ProjectSnapshot.ServiceProvided = contract.ServiceProvided;
            invoice.ProjectSnapshot.HourlyRate = contract.HourlyRate;
            invoice.ProjectSnapshot.BillingCycle = contract.BillingCycle;
            invoice.TotalAmount = totals.TotalAmount;
            invoice.TotalMinutesWorked = totals.TotalMinutesWorked;
            invoice.TotalDaysWorked = totals.TotalDaysWorked;
            invoice.AverageRevenuePerDay = totals.AverageRevenuePerDay;
            invoice.UpdatedAt = now;

            await database.Invoices.ReplaceOneAsync(x => x.Id == invoice.Id, invoice);
        }
    }

    private async Task<decimal> ResolveHourlyRateAsync(IClientSessionHandle mongoSession, InvoiceEntity invoice)
    {
        if (!ObjectId.TryParse(invoice.ProjectSnapshot.ContractId, out var contractId))
            return invoice.ProjectSnapshot.HourlyRate;
        var contract = await database.BillingContracts
            .Find(mongoSession, x => x.Id == contractId && x.OrganizationId == invoice.OrganizationId)
            .FirstOrDefaultAsync();
        return contract?.HourlyRate ?? invoice.ProjectSnapshot.HourlyRate;
    }

    public static bool CanTransitionStatus(string currentStatus, string nextStatus)
    {
        return Lifecycle.CanTransition(currentStatus, nextStatus);
    }
}