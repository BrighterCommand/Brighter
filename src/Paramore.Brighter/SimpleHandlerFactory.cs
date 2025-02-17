using System;

namespace Paramore.Brighter;

/// <summary>
/// A simple handler factory that creates a handler for a given request type.
/// Intended for use with tests, where you want to create a handler for a given request type
/// </summary>
/// <param name="asyncFactory"></param>
/// <param name="factory"></param>
public class SimpleHandlerFactory(Func<Type, IHandleRequests> factory, Func<Type, IHandleRequestsAsync> asyncFactory)
    : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
{
    IHandleRequests IAmAHandlerFactorySync.Create(Type handlerType, IAmALifetime lifetime)
        => factory(handlerType);

    /// <inheritdoc />
    public void Release(IHandleRequestsAsync? handler, IAmALifetime lifetime)
    {
        if (handler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <inheritdoc />
    public void Release(IHandleRequests handler, IAmALifetime lifetime)
    {
        if (handler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetime)
        => asyncFactory(handlerType);
}
