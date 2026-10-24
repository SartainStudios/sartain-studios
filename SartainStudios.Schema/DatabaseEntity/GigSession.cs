using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class GigSession : AuditableEntity
{
    private DateTime? _endTime;

    public ObjectId UserId { get; init; }
    public DateTime StartTime { get; set; }

    public DateTime? EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            IsOpen = value is null;
        }
    }

    public bool IsOpen { get; set; } = true;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? StartOdometer { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? EndOdometer { get; set; }

    public string VehicleType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<GigEarningEntry> Earnings { get; set; } = [];
    public List<GigExpenseEntry> Expenses { get; set; } = [];
}

public sealed class GigEarningEntry
{
    public ObjectId SourceId { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public string Note { get; set; } = string.Empty;
}

public sealed class GigExpenseEntry
{
    public ObjectId CategoryId { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public string Note { get; set; } = string.Empty;
}