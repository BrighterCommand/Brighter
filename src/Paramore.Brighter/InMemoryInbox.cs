#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// An item in the inbox - a message that we have received
    /// </summary>
    public class InboxItem : IHaveABoxWriteTime
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InboxItem"/> class.
        /// </summary>
        /// <param name="requestType">Type of the request.</param>
        /// <param name="requestBody">The request body.</param>
        /// <param name="writeTime">The request arrived at when.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        public InboxItem(Type requestType, string requestBody, DateTimeOffset writeTime, string contextKey)
        {
            RequestType = requestType;
            RequestBody = requestBody;
            WriteTime = writeTime;
            Key = contextKey;
        }

        /// <summary>
        /// Gets or sets the command body.
        /// </summary>
        /// <value>The command body.</value>
        public string RequestBody { get; set; }                                                                           
        
        /// <summary>
        /// Gets the type of the command.
        /// </summary>
        /// <value>The type of the command.</value>
        public Type RequestType { get; }

        /// <summary>
        /// Gets the command when.
        /// </summary>
        /// <value>The command when.</value>

        public DateTimeOffset WriteTime { get; }

        /// <summary>
        /// The Id and the key for the context i.e. message type, that we are looking for
        /// Occurs because we may service the same message in different contexts and need to
        /// know they are all handled or not
        /// </summary>
        string Key { get;}

        /// <summary>
        /// Convert a Guid identity and context into a key, convenience wrapper
        /// </summary>
        /// <param name="id">The Guid for the request</param>
        /// <param name="contextKey">The handler this is for</param>
        /// <returns></returns>
        public static string CreateKey(string id, string contextKey)
        {
            return $"{id}:{contextKey}";
        }
    }
    
    
    /// <summary>
    /// Class InMemoryInbox.
    /// A Inbox stores <see cref="Command"/>s for diagnostics or replay.
    /// This class is intended to be thread-safe, so you can use one InMemoryInbox across multiple performers. However, the state is not global i.e. static
    /// so you can use multiple instances safely as well.
    /// N.B. that the primary limitation of this in-memory inbox is that it will not work across processes. So if you use the competing consumers pattern
    /// the consumers will not be able to determine if another consumer has already processed this command.
    /// It is possible to use multiple performers within one process as competing consumers, and if you want to use an InMemoryInbox this is the most
    /// viable strategy - otherwise use an out-of-process inbox that provides shared state to all consumers
    /// </summary>
    public class InMemoryInbox(TimeProvider timeProvider,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        : InMemoryBox<InboxItem>(timeProvider), IAmAnInboxSync, IAmAnInboxAsync
    {
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly InstrumentationOptions _instrumentationOptions = instrumentationOptions;

        /// <inheritdoc />
        public bool ContinueOnCapturedContext { get; set; }

        /// <inheritdoc />
        public IAmABrighterTracer? Tracer { private get; set; }

        /// <summary>
        ///   Adds a command to the in-memory store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        public void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) 
            where T : class, IRequest
        {
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Brighter, InMemoryAttributes.InboxDbName, BoxDbOperation.Add, InMemoryAttributes.DbTable),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                ClearExpiredMessages();

                string key = InboxItem.CreateKey(command.Id, contextKey);
                if (!ExistsInternal<T>(command.Id, contextKey))
                {
                    if (!Requests.TryAdd(key, new InboxItem(typeof(T), string.Empty, _timeProvider.GetUtcNow().DateTime, contextKey)))
                    {
                        throw new Exception($"Could not add command: {command.Id} to the Inbox");
                    }
                }

                Requests[key].RequestBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        ///   Awaitably adds a command to the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
        /// <returns><see cref="Task"/>.</returns>
        public Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext, 
            int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default) 
            where T : class, IRequest
        {
            // Note: Don't create a span here - we call the sync method behind the scenes
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            Add(command, contextKey, requestContext, timeoutInMilliseconds);

            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        ///   Finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        /// <returns><see cref="T"/></returns>
        public T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) 
            where T : class, IRequest
        {
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Brighter, InMemoryAttributes.InboxDbName, BoxDbOperation.Get, InMemoryAttributes.DbTable),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                ClearExpiredMessages();

                if (Requests.TryGetValue(InboxItem.CreateKey(id, contextKey), out InboxItem? inboxItem))
                {
                    var result = JsonSerializer.Deserialize<T>(inboxItem.RequestBody, JsonSerialisationOptions.Options);

                    if (result is null) throw new ArgumentException("Body must not be null");
                    return result;
                }

                throw new RequestNotFoundException<T>(id);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        ///   Checks whether a command with the specified identifier exists in the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        /// <returns><see langword="true"/> if it exists, otherwise <see langword="false"/>.</returns>
        public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Brighter, InMemoryAttributes.InboxDbName, BoxDbOperation.Exists, InMemoryAttributes.DbTable),
                requestContext?.Span,
                options: _instrumentationOptions
            );

            try
            {
                return ExistsInternal<T>(id, contextKey);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        ///   Awaitable checks whether a command with the specified identifier exists in the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
        /// <returns><see cref="Task{true}"/> if it exists, otherwise <see cref="Task{false}"/>.</returns>
        public Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            // Note: Don't create a span here - we call the sync method behind the scenes
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Exists<T>(id, contextKey, requestContext, timeoutInMilliseconds);

            tcs.SetResult(command);
            return tcs.Task;
        }

        /// <summary>
        ///   Awaitably finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Ignored as commands are stored in-memory</param>
        /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
        /// <returns><see cref="Task{T}"/>.</returns>
        public Task<T> GetAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            // Note: Don't create a span here - we call the sync method behind the scenes
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get<T>(id, contextKey, requestContext, timeoutInMilliseconds);

            tcs.SetResult(command);
            return tcs.Task;
        }

        // Performs the logic of checking whether a command exists in the inbox without creating telemetry
        private bool ExistsInternal<T>(string id, string contextKey)
        {
            ClearExpiredMessages();

            return Requests.ContainsKey(InboxItem.CreateKey(id, contextKey));
        }
    }
}
