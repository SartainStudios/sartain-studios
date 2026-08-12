using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class Membership : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int? WeeklyHourLimitMinutes { get; set; }
    public int HourLimitWarningMinutes { get; set; } = 30;
}