using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SartainStudios.Schema.DatabaseEntity;

public sealed class GigSettings : AuditableEntity
{
    public ObjectId UserId { get; init; }
    public ObjectId[] HiddenSourceIds { get; set; } = [];
    public ObjectId[] HiddenCategoryIds { get; set; } = [];
    public bool MileageDeductionEnabled { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? MileageRateOverride { get; set; }
}