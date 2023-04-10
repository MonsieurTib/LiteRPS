using System.Collections.Concurrent;

namespace LiteRPS;

internal sealed class SubscriptionManager
{
    private readonly ConcurrentDictionary<string, HandlerList> _handlers = new();
    public List<string> GetSubscribedTopics => _handlers.Keys.ToList();

    public IDisposable Add<T>(string key, Func<T, Task> callback)
    {
        return Add(key, parameters =>
        {
            callback((T)parameters);
            return Task.CompletedTask;
        });
    }
    
    private IDisposable Add(string key, Func<object, Task> callback)
    {
        var handler = new Handler(callback);
        var handlerList = _handlers.AddOrUpdate(key, _ => new HandlerList(handler), (_, list) =>
        {
            lock (list)
            {
                list.AddHandler(handler);
            }

            return list;
        });

        return new Subscription(handlerList, handler);
    }

    public async Task PublishAsync(string key, object msg)
    {
        if (!_handlers.TryGetValue(key, out var handlerList))
        {
            return;
        }

        foreach (var handler in handlerList.Handlers)
        {
            await handler.InvokeAsync(msg);
        }
    }
}

internal sealed class HandlerList
{
    public HandlerList(Handler handler)
    {
        Handlers = new List<Handler> { handler };
    }

    public List<Handler> Handlers { get; }

    public void RemoveHandler(Handler handler) => Handlers.Remove(handler);

    public void AddHandler(Handler handler) => Handlers.Add(handler);
}

internal sealed class Handler
{
    private readonly Func<object, Task> _callback;

    public Handler(Func<object, Task> callback)
    {
        _callback = callback;
    }

    public Task InvokeAsync(object parameters)
    {
        return _callback(parameters);
    }
}

internal sealed class Subscription : IDisposable
{
    private readonly Handler _handler;
    private readonly HandlerList _handlerList;
    private bool _disposed;

    public Subscription(HandlerList handlerList, Handler handler)
    {
        _handlerList = handlerList;
        _handler = handler;
    }
 
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _handlerList.RemoveHandler(_handler);
    }
}