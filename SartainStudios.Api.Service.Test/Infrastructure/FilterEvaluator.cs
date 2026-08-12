using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal static class FilterEvaluator
{
    public static List<TDocument> Apply<TDocument>(FilterDefinition<TDocument>? filter,
        IEnumerable<TDocument> documents)
    {
        var candidates = documents.ToList();

        if (filter is null || filter == FilterDefinition<TDocument>.Empty) return candidates;

        if (filter is ExpressionFilterDefinition<TDocument> expressionFilter)
            return candidates.Where(expressionFilter.Expression.Compile()).ToList();

        var rendered = Render(filter);

        return candidates.Where(document => Matches(rendered, document.ToBsonDocument())).ToList();
    }

    private static BsonDocument Render<TDocument>(FilterDefinition<TDocument> filter)
    {
        var serializer = BsonSerializer.LookupSerializer<TDocument>();

        return filter.Render(new RenderArgs<TDocument>(serializer, BsonSerializer.SerializerRegistry));
    }

    private static bool Matches(BsonDocument query, BsonDocument document)
    {
        return query.Elements.All(element => MatchesElement(element, document));
    }

    private static bool MatchesElement(BsonElement element, BsonDocument document)
    {
        switch (element.Name)
        {
            case "$and":
                return element.Value.AsBsonArray.All(clause => Matches(clause.AsBsonDocument, document));
            case "$or":
                return element.Value.AsBsonArray.Any(clause => Matches(clause.AsBsonDocument, document));
            case "$nor":
                return !element.Value.AsBsonArray.Any(clause => Matches(clause.AsBsonDocument, document));
        }

        var actual = ResolveField(document, element.Name);

        if (element.Value is BsonDocument operators && operators.Elements.All(x => x.Name.StartsWith('$')))
            return operators.Elements.All(op => MatchesOperator(op, actual));

        return actual is not null && actual == element.Value;
    }

    private static bool MatchesOperator(BsonElement op, BsonValue? actual)
    {
        return op.Name switch
        {
            "$eq" => actual is not null && actual == op.Value,
            "$ne" => actual is null || actual != op.Value,
            "$in" => actual is not null && op.Value.AsBsonArray.Contains(actual),
            "$nin" => actual is null || !op.Value.AsBsonArray.Contains(actual),
            "$exists" => op.Value.AsBoolean == actual is not null,
            "$gt" => actual is not null && actual.CompareTo(op.Value) > 0,
            "$gte" => actual is not null && actual.CompareTo(op.Value) >= 0,
            "$lt" => actual is not null && actual.CompareTo(op.Value) < 0,
            "$lte" => actual is not null && actual.CompareTo(op.Value) <= 0,
            "$not" => !MatchesOperator(op.Value.AsBsonDocument.GetElement(0), actual),
            _ => throw new NotSupportedException($"The test filter evaluator does not support '{op.Name}'.")
        };
    }

    private static BsonValue? ResolveField(BsonDocument document, string path)
    {
        BsonValue current = document;

        foreach (var segment in path.Split('.'))
        {
            if (current is not BsonDocument nested || !nested.TryGetValue(segment, out var value)) return null;

            current = value;
        }

        return current;
    }
}