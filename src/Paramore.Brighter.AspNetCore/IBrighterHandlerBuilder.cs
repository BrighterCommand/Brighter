using System;
using System.Reflection;

namespace Paramore.Brighter.AspNetCore
{
    public interface IBrighterHandlerBuilder
    {
        IBrighterHandlerBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers);
        IBrighterHandlerBuilder HandlersFromAssemblies(params Assembly[] assemblies);
        IBrighterHandlerBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers);
        IBrighterHandlerBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies);
    }
}