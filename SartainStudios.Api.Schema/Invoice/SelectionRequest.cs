using MongoDB.Bson;

namespace SartainStudios.Api.Schema.Invoice;

public sealed record SelectionRequest(ObjectId ContractId, IReadOnlyList<ObjectId> SessionIds);