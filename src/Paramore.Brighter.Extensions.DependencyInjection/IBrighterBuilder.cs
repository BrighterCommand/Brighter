#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
 
#endregion

using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Constructs Brighter message mappers and handlers
    /// </summary>
    public interface IBrighterBuilder
    {
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests, IHandleRequestsAsync, IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns></returns>
        IBrighterBuilder AutoFromAssemblies(params Assembly[] assemblies);
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequestsAsync and register them with ServiceCollection
        /// </summary>
        /// <param name="registerHandlers">A callback to register handlers</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers);
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests and register them with ServiceCollection 
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies);
        
        /// <summary>
        /// Register handlers with the built in subscriber registry
        /// </summary>
        /// <param name="registerHandlers">A callback to register handlers</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers);
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IHandleRequests and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder HandlersFromAssemblies(params Assembly[] assemblies);

        /// <summary>
        /// Register message mappers
        /// </summary>
        /// <param name="registerMappers">A callback to register mappers</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder MapperRegistry(Action<ServiceCollectionMessageMapperRegistry> registerMappers);
        
        /// <summary>
        /// Scan the assemblies provided for implementations of IAmAMessageMapper and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies);

        /// <summary>
        /// Scan the assemblies for implementations of IAmAMessageTransformAsync and register them with ServiceCollection
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>This builder, allows chaining calls</returns>
        IBrighterBuilder TransformsFromAssemblies(params Assembly[] assemblies);

        /// <summary>
        /// The policy registry to use for the command processor and the event bus
        /// It needs to be here as we need to pass it between AddBrighter and UseExternalBus
        /// </summary>
        IPolicyRegistry<string> PolicyRegistry { get; set; }

        /// <summary>
        /// The IoC container to populate
        /// </summary>
        IServiceCollection Services { get; }

    }
}
