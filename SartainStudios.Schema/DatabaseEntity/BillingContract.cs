using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class BillingContract : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId ProjectId { get; set; }
    public decimal HourlyRate { get; set; }
    public int ExpectedMinutes { get; set; }
    public string BillingCycle { get; set; } = string.Empty;
    public string ServiceProvided { get; set; } = string.Empty;
    public string InvoicePrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}