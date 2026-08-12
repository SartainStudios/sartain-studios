using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Castle.DynamicProxy;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal sealed class FakeCollection<TDocument> where TDocument : class
{
    private static readonly ProxyGenerator ProxyGenerator = new();

    private readonly IMongoCollection<TDocument> _substitute;

    public FakeCollection()
    {
        _substitute = Substitute.For<IMongoCollection<TDocument>>();

        StubFind();
        StubCount();
        StubInsert();
        StubReplace();
        StubDelete();

        Collection = ProxyGenerator.CreateInterfaceProxyWithTarget(_substitute, new ProjectionInterceptor(this));
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
        _substitute
            .FindSync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(0)));

        _substitute
            .FindSync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(1)));

        _substitute
            .FindAsync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(0)));

        _substitute
            .FindAsync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, TDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Cursor(call.ArgAt<FilterDefinition<TDocument>>(1)));

        _substitute
            .FindSync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(0)));

        _substitute
            .FindSync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(1)));

        _substitute
            .FindAsync(Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(0)));

        _substitute
            .FindAsync(Arg.Any<IClientSessionHandle>(),
                Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<FindOptions<TDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => CursorBson(call.ArgAt<FilterDefinition<TDocument>>(1)));
    }

    private void StubInsert()
    {
        _substitute
            .InsertOneAsync(Arg.Any<TDocument>(), Arg.Any<InsertOneOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Insert(call.ArgAt<TDocument>(0)));

        _substitute
            .InsertOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<TDocument>(), Arg.Any<InsertOneOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Insert(call.ArgAt<TDocument>(1)));
    }

    private void StubCount()
    {
        _substitute
            .CountDocumentsAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CountOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult((long)FilterEvaluator
                .Apply(call.ArgAt<FilterDefinition<TDocument>>(0), Documents)
                .Count));

        _substitute
            .CountDocumentsAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<CountOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult((long)FilterEvaluator
                .Apply(call.ArgAt<FilterDefinition<TDocument>>(1), Documents)
                .Count));
    }

    private void StubReplace()
    {
        _substitute
            .ReplaceOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<TDocument>(),
                Arg.Any<ReplaceOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Replace(call.ArgAt<FilterDefinition<TDocument>>(0), call.ArgAt<TDocument>(1)));

        _substitute
            .ReplaceOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<TDocument>(), Arg.Any<ReplaceOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Replace(call.ArgAt<FilterDefinition<TDocument>>(1), call.ArgAt<TDocument>(2)));
    }

    private void StubDelete()
    {
        _substitute
            .DeleteOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), true));

        _substitute
            .DeleteOneAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<DeleteOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), true));

        _substitute
            .DeleteOneAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<FilterDefinition<TDocument>>(),
                Arg.Any<DeleteOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(1), true));

        _substitute
            .DeleteManyAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), false));

        _substitute
            .DeleteManyAsync(Arg.Any<FilterDefinition<TDocument>>(), Arg.Any<DeleteOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Delete(call.ArgAt<FilterDefinition<TDocument>>(0), false));

        _substitute
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

    private sealed class ProjectionInterceptor(FakeCollection<TDocument> owner) : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            if (!TryProject(invocation)) invocation.Proceed();
        }

        private bool TryProject(IInvocation invocation)
        {
            var method = invocation.Method;

            if (method.Name is not ("FindAsync" or "FindSync") || !method.IsGenericMethod) return false;

            var projectionType = method.GetGenericArguments()[0];

            if (projectionType == typeof(TDocument)) return false;

            var offset = invocation.Arguments.Length == 4 ? 1 : 0;

            if (invocation.Arguments[offset] is not FilterDefinition<TDocument> filter) return false;

            var projection = Projection(invocation.Arguments[offset + 1]);

            if (projection is null) return false;

            var matches = FilterEvaluator.Apply(filter, owner.Documents);
            var cursor = Cursor(projectionType, projection.Compile(), matches);

            invocation.ReturnValue = method.Name == "FindSync" ? cursor : Completed(projectionType, cursor);

            return true;
        }

        private static LambdaExpression? Projection(object? options)
        {
            var definition = options?
                .GetType()
                .GetProperty("Projection", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(options);

            if (definition is null) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fromProperty = definition
                .GetType()
                .GetProperties(flags)
                .Where(property => typeof(LambdaExpression).IsAssignableFrom(property.PropertyType))
                .Select(property => property.GetValue(definition))
                .FirstOrDefault(value => value is not null);

            if (fromProperty is LambdaExpression lambda) return lambda;

            return definition
                .GetType()
                .GetFields(flags)
                .Where(field => typeof(LambdaExpression).IsAssignableFrom(field.FieldType))
                .Select(field => field.GetValue(definition))
                .OfType<LambdaExpression>()
                .FirstOrDefault();
        }

        private static object Cursor(Type projectionType, Delegate projection, IEnumerable<TDocument> documents)
        {
            var projected = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(projectionType))!;

            foreach (var document in documents) projected.Add(projection.DynamicInvoke(document));

            return Activator.CreateInstance(typeof(FakeAsyncCursor<>).MakeGenericType(projectionType), projected)!;
        }

        private static object Completed(Type projectionType, object cursor)
        {
            return typeof(Task)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(candidate => candidate is { Name: nameof(Task.FromResult), IsGenericMethodDefinition: true })
                .MakeGenericMethod(typeof(IAsyncCursor<>).MakeGenericType(projectionType))
                .Invoke(null, [cursor])!;
        }
    }
}