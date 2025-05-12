namespace AzureAiSearchWebsiteCrawler.Models;

using System.Collections.Concurrent;

public class ItemQueue<T>()
{
    private readonly ConcurrentQueue<T> _queue = new();

    private volatile bool _completed = false;

    public void Enqueue(T item) => _queue.Enqueue(item);

    public bool TryDequeue(out T item) => _queue.TryDequeue(out item);

    public void MarkCompleted() => _completed = true;

    public bool IsCompleted => _completed && _queue.IsEmpty;

    public bool IsEmpty => _queue.IsEmpty;
}