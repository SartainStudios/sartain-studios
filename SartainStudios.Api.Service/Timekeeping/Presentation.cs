using SartainStudios.Schema.DatabaseEntity;
using History = SartainStudios.Schema.WorkSession.History;
using Session = SartainStudios.Schema.WorkSession.Session;
using State = SartainStudios.Schema.WorkSession.State;
using Status = SartainStudios.Schema.Invoice.Status;
using WorkSessionEntity = SartainStudios.Schema.DatabaseEntity.WorkSession;

namespace SartainStudios.Api.Service.Timekeeping;

public static class Presentation
{
    public static History ToHistory(WorkSessionEntity session, BillingContract? contract,
        SartainStudios.Schema.DatabaseEntity.Project? project,
        string? invoiceStatus, DateTime now)
    {
        var isRunning = session.EndTime is null;
        var isEditableInvoice = !session.InvoiceId.HasValue ||
                                string.Equals(invoiceStatus, nameof(Status.Draft), StringComparison.OrdinalIgnoreCase);
        return new History(
            session.Id.ToString(),
            session.OrganizationId.ToString(),
            session.UserId.ToString(),
            session.ContractId.ToString(),
            session.ProjectId.ToString(),
            project?.Name ?? string.Empty,
            contract?.ServiceProvided ?? string.Empty,
            session.InvoiceId?.ToString(),
            session.StartTime,
            session.EndTime,
            Timing.ElapsedMinutes(session.StartTime, session.EndTime ?? now),
            isRunning,
            isEditableInvoice,
            isEditableInvoice);
    }

    public static Session ToSession(WorkSessionEntity session, BillingContract? contract,
        SartainStudios.Schema.DatabaseEntity.Project? project,
        DateTime now)
    {
        return new Session(
            session.Id.ToString(),
            session.OrganizationId.ToString(),
            session.UserId.ToString(),
            session.ContractId.ToString(),
            session.ProjectId.ToString(),
            project?.Name ?? string.Empty,
            contract?.ServiceProvided ?? string.Empty,
            session.StartTime,
            session.EndTime,
            Timing.ElapsedMinutes(session.StartTime, session.EndTime ?? now));
    }

    public static State ToState(WorkSessionEntity? session, BillingContract? contract,
        SartainStudios.Schema.DatabaseEntity.Project? project)
    {
        var now = DateTime.UtcNow;
        return session is null
            ? new State(false, null, now)
            : new State(true, ToSession(session, contract, project, now), now);
    }
}