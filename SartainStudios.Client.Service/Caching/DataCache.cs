using System.Collections;
using System.Collections.Concurrent;

namespace SartainStudios.Client.Service.Caching;

public sealed class DataCache(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public DataCache() : this(TimeProvider.System)
    {
    }

    public event Action<string>? Changed;

    public Task<T> GetOrFetchAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CachePolicy? policy = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var effectivePolicy = policy ?? CachePolicy.Default;
        var entry = _entries.GetOrAdd(key, static _ => new Entry());

        if (!forceRefresh)
        {
            var now = timeProvider.GetUtcNow();
            lock (entry.Gate)
            {
                if (entry.HasValue && entry.Value is T cached && now - entry.StoredAt < effectivePolicy.ExpiresAfter)
                {
                    if (now - entry.StoredAt >= effectivePolicy.StaleAfter)
                        BeginRevalidate(key, entry, factory);
                    return Task.FromResult(cached);
                }

                if (entry.Pending is Task<T> pending)
                    return pending;
            }
        }

        return StartLoadAsync(entry, factory, cancellationToken);
    }

    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var entry = _entries.GetOrAdd(key, static _ => new Entry());
        lock (entry.Gate)
        {
            entry.Value = value;
            entry.HasValue = true;
            entry.StoredAt = timeProvider.GetUtcNow();
        }
    }

    public void Invalidate(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _entries.TryRemove(key, out _);
    }

    public void InvalidatePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return;
        foreach (var key in _entries.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _entries.TryRemove(key, out _);
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private Task<T> StartLoadAsync<T>(
        Entry entry,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<T> completion;
        lock (entry.Gate)
        {
            if (entry.Pending is Task<T> pending) return pending;
            completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            entry.Pending = completion.Task;
        }

        _ = FetchAsync(entry, factory, completion, cancellationToken);
        return completion.Task;
    }

    private async Task FetchAsync<T>(
        Entry entry,
        Func<CancellationToken, Task<T>> factory,
        TaskCompletionSource<T> completion,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await factory(cancellationToken).ConfigureAwait(false);
            lock (entry.Gate)
            {
                entry.Value = value;
                entry.HasValue = true;
                entry.StoredAt = timeProvider.GetUtcNow();
                entry.Pending = null;
            }

            completion.TrySetResult(value);
        }
        catch (OperationCanceledException exception)
        {
            ClearPending(entry);
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            ClearPending(entry);
            completion.TrySetException(exception);
        }
    }

    private static void ClearPending(Entry entry)
    {
        lock (entry.Gate)
        {
            entry.Pending = null;
        }
    }

    private void BeginRevalidate<T>(string key, Entry entry, Func<CancellationToken, Task<T>> factory)
    {
        if (entry.IsRevalidating) return;
        entry.IsRevalidating = true;
        _ = RevalidateAsync(key, entry, factory);
    }

    private async Task RevalidateAsync<T>(string key, Entry entry, Func<CancellationToken, Task<T>> factory)
    {
        try
        {
            var value = await factory(CancellationToken.None).ConfigureAwait(false);
            bool changed;
            lock (entry.Gate)
            {
                changed = !entry.HasValue || !ValuesEqual(entry.Value, value);
                entry.Value = value;
                entry.HasValue = true;
                entry.StoredAt = timeProvider.GetUtcNow();
            }

            if (changed) Changed?.Invoke(key);
        }
        catch
        {
            // A failed background refresh must never surface to the user; the stale value stays in place and the
            // next expiry forces a foreground fetch.
        }
        finally
        {
            entry.IsRevalidating = false;
        }
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (Equals(left, right)) return true;
        if (left is not IEnumerable leftItems || right is not IEnumerable rightItems) return false;
        return leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>());
    }

    private sealed class Entry
    {
        public Lock Gate { get; } = new();
        public object? Value { get; set; }
        public bool HasValue { get; set; }
        public DateTimeOffset StoredAt { get; set; }
        public object? Pending { get; set; }
        public volatile bool IsRevalidating;
    }
}