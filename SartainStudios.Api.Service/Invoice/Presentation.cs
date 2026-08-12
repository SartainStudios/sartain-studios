using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Invoice;
using ClientSnapshot = SartainStudios.Schema.Client.Snapshot;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using OrganizationEntity = SartainStudios.Schema.DatabaseEntity.Organization;
using OrganizationSnapshot = SartainStudios.Schema.Organization.Snapshot;
using ProjectSnapshot = SartainStudios.Schema.Project.Snapshot;
using Summary = SartainStudios.Schema.Invoice.Summary;

namespace SartainStudios.Api.Service.Invoice;

public static class Presentation
{
    public static Detail ToDetail(InvoiceEntity invoice, IReadOnlyList<WorkSession> sessions)
    {
        var dailyBreakdown = Totals.CalculateDailyBreakdown(sessions, invoice.ProjectSnapshot.HourlyRate);
        return new Detail(
            invoice.Id.ToString(),
            invoice.OrganizationId.ToString(),
            invoice.ClientId.ToString(),
            invoice.InvoiceNumber,
            invoice.OrganizationSnapshot,
            invoice.ClientSnapshot,
            invoice.ProjectSnapshot,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.TotalMinutesWorked,
            invoice.TotalDaysWorked,
            invoice.AverageRevenuePerDay,
            invoice.Status,
            invoice.BilledSessionIds.Select(x => x.ToString()).ToList(),
            dailyBreakdown,
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }

    public static Summary ToSummary(InvoiceEntity invoice)
    {
        return new Summary(
            invoice.Id.ToString(),
            invoice.OrganizationId.ToString(),
            invoice.ClientId.ToString(),
            invoice.InvoiceNumber,
            invoice.ClientSnapshot.CompanyName,
            invoice.ProjectSnapshot.ProjectName,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.TotalMinutesWorked,
            invoice.TotalDaysWorked,
            invoice.AverageRevenuePerDay,
            invoice.Status,
            invoice.BilledSessionIds.Select(x => x.ToString()).ToList(),
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }

    public static SelectableSession ToSelectableSession(WorkSession session,
        SartainStudios.Schema.DatabaseEntity.Project project, BillingContract contract)
    {
        var minutesWorked = Math.Max(0, (int)Math.Floor((session.EndTime!.Value - session.StartTime).TotalMinutes));
        return new SelectableSession(
            session.Id.ToString(),
            session.OrganizationId.ToString(),
            session.UserId.ToString(),
            session.ContractId.ToString(),
            session.ProjectId.ToString(),
            project.Name,
            contract.ServiceProvided,
            session.StartTime,
            session.EndTime!.Value,
            minutesWorked);
    }

    public static OrganizationSnapshot ToOrganizationSnapshot(OrganizationEntity organization)
    {
        return new OrganizationSnapshot
        {
            Name = organization.Name,
            Address = organization.Address,
            Email = organization.Email,
            PhoneNumber = organization.PhoneNumber
        };
    }

    public static ClientSnapshot ToClientSnapshot(SartainStudios.Schema.DatabaseEntity.Client client)
    {
        return new ClientSnapshot
        {
            CompanyName = client.CompanyName,
            ContactPerson = client.ContactPerson,
            Address = client.Address,
            Email = client.Email,
            PhoneNumber = client.PhoneNumber
        };
    }

    public static ProjectSnapshot ToProjectSnapshot(SartainStudios.Schema.DatabaseEntity.Project project,
        BillingContract contract)
    {
        return new ProjectSnapshot
        {
            ProjectName = project.Name,
            ProjectDescription = project.Description,
            ServiceProvided = contract.ServiceProvided,
            HourlyRate = contract.HourlyRate,
            BillingCycle = contract.BillingCycle,
            ContractId = contract.Id.ToString(),
            ProjectId = project.Id.ToString()
        };
    }
}