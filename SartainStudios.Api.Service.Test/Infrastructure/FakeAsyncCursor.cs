using MongoDB.Driver;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal sealed class FakeAsyncCursor<TDocument>(IReadOnlyList<TDocument> documents) : IAsyncCursor<TDocument>
{
    private bool _consumed;

    public IEnumerable<TDocument> Current { get; private set; } = [];

    public bool MoveNext(CancellationToken cancellationToken = default)
    {
        if (_consumed || documents.Count == 0) return false;

        _consumed = true;
        Current = documents;

        return true;
    }

    public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MoveNext(cancellationToken));
    }

    public void Dispose()
    {
    }
}