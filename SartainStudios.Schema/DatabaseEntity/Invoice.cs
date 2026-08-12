using MongoDB.Bson;
using SartainStudios.Schema.Organization;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class Invoice : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId ClientId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime DueDate { get; set; }
    public Snapshot OrganizationSnapshot { get; init; } = new();
    public Schema.Client.Snapshot ClientSnapshot { get; init; } = new();
    public Schema.Project.Snapshot ProjectSnapshot { get; init; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalMinutesWorked { get; set; }
    public int TotalDaysWorked { get; set; }
    public decimal AverageRevenuePerDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public ObjectId[] BilledSessionIds { get; set; } = [];
}