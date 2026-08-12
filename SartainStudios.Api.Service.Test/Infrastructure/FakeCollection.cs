using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal sealed class FakeCollection<TDocument> where TDocument : class
{
    public FakeCollection()
    {
        Collection = Substitute.For<IMongoCollection<TDocument>>();

        StubFind();
        StubCount();
        StubInsert();
        StubReplace();
        StubDelete();
    }

    public IMongoCollection<TDocument> Collection { get; }

    public List<TDocument> Documents { get; } = [];

    public List<TDocument> Inserted { get; } = [];

    public List<TDocument> Replaced { get; } = [];

    public int DeletedDocumentCount { get; private set; }

    public Exception? WriteFailure { get; set; }

    public FakeCollection<TDocument> Seed(params TDocument[] documents)
    {
        Documents.AddRange(documents);

        return this;
    }

    private void StubFind()
    {
        Collection
            .FindSync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(0)));

        Collection
            .FindSync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(1)));

        Collection
            .FindAsync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(0)));

        Collection
            .FindAsync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(1)));

        Collection
            .FindSync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(0)));

        Collection
            .FindSync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(1)));

        Collection
            .FindAsync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(0)));

        Collection
            .FindAsync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(1)));
    }

    private void StubInsert()
    {
        Collection
            .InsertOneAsync(Arg.Any<TDocument>(), Arg.Any<InsertOneOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Insert(call.ArgAt<TDocument>(0)));

        Collection
            .InsertOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<TDocument>(), Arg.Any<InsertOneOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Insert(call.ArgAt<TDocument>(1)));
    }

    private void StubCount()
    {
        Collection
            .CountDocumentsAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CountOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult((long)FilterEvaluator
                .Apply(call.ArgAt<FilterDefinition<TDocument>>(0), Documents)
                .Count));

        Collection
            .CountDocumentsAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<CountOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult((long)FilterEvaluator
                .Apply(call.ArgAt<FilterDefinition<TDocument>>(1), Documents)
                .Count));
    }

    private void StubReplace()
    {
        Collection
            .ReplaceOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<TDocument>(),
                Arg.Any<ReplaceOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Replace(call.ArgAt<FilterDefinition<TDocument>>(0), call.ArgAt<TDocument>(1)));

        Collection
            .ReplaceOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<TDocument>(), Arg.Any<ReplaceOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Replace(call.ArgAt<FilterDefinition<TDocument>>(1), call.ArgAt<TDocument>(2)));
    }

    private void StubDelete()
    {
        Collection
            .DeleteOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), true));

        Collection
            .DeleteOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<DeleteOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), true));

        Collection
            .DeleteOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<DeleteOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(1), true));

        Collection
            .DeleteManyAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), false));

        Collection
            .DeleteManyAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<DeleteOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), false));

        Collection
            .DeleteManyAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<DeleteOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(1), false));
    }

    private IAsyncCursor<TDocument> Cursor(FilterDefinition<TDocument> filter)
    {
        return new FakeAsyncCursor<TDocument>(FilterEvaluator.Apply(filter, Documents));
    }

    private IAsyncCursor<BsonDocument> CursorBson(FilterDefinition<TDocument> filter)
    {
        return new FakeAsyncCursor<BsonDocument>(FilterEvaluator
            .Apply(filter, Documents)
            .Select(x => x.ToBsonDocument())
            .ToList());
    }

    private Task Insert(TDocument document)
    {
        Guard();
        Documents.Add(document);
        Inserted.Add(document);

        return Task.CompletedTask;
    }

    private Task<ReplaceOneResult> Replace(FilterDefinition<TDocument> filter, TDocument replacement)
    {
        Guard();

        var matches = FilterEvaluator.Apply(filter, Documents);

        if (matches.Count > 0)
        {
            Documents[Documents.IndexOf(matches[0])] = replacement;
            Replaced.Add(replacement);
        }

        return Task.FromResult<ReplaceOneResult>(
            new ReplaceOneResult.Acknowledged(matches.Count == 0 ? 0 : 1, matches.Count == 0 ? 0 : 1, null));
    }

    private Task<DeleteResult> Delete(FilterDefinition<TDocument> filter, bool single)
    {
        Guard();

        var matches = FilterEvaluator.Apply(filter, Documents);

        if (single) matches = matches.Take(1).ToList();

        foreach (var match in matches) Documents.Remove(match);

        DeletedDocumentCount += matches.Count;

        return Task.FromResult<DeleteResult>(new DeleteResult.Acknowledged(matches.Count));
    }

    private void Guard()
    {
        if (WriteFailure is not null) throw WriteFailure;
    }
}