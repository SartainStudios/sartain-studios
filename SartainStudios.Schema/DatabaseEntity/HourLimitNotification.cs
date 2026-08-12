using MongoDB.Bson;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class HourLimitNotification : AuditableEntity
{
    public ObjectId OrganizationId { get; init; }
    public ObjectId UserId { get; init; }
    public DateTime WeekStart { get; init; }
    public string NotificationType { get; init; } = string.Empty;
}