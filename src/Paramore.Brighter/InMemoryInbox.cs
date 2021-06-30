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
        private InboxItem(Type requestType, string requestBody, DateTime writeTime, string contextKey)
        {
            RequestType = requestType;
            RequestBody = requestBody;
            WriteTime = writeTime;
            Key = contextKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InboxItem"/> class.
        /// </summary>
        /// <param name="requestType">Type of the command.</param>
        /// <param name="requestBody">The command body.</param>
        public InboxItem(Type requestType, string requestBody, string contextKey)
            : this(requestType, requestBody, DateTime.UtcNow, contextKey) {}

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

        public DateTime WriteTime { get; }

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
        public static string CreateKey(Guid id, string contextKey)
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
    public class InMemoryInbox : InMemoryBox<InboxItem>, IAmAnInbox, IAmAnInboxAsync
    {
        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext
        /// </summary>
        /// <value><c>true</c> if [continue on captured context]; otherwise, <c>false</c>.</value>
        public bool ContinueOnCapturedContext { get; set; }
        
        /// <summary>
        /// Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey"></param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            ClearExpiredMessages();
            
            string key = InboxItem.CreateKey(command.Id, contextKey);
            if (!Exists<T>(command.Id, contextKey))
            {
                if (!_requests.TryAdd(key, new InboxItem(typeof (T), string.Empty, contextKey)))
                {
                    throw new Exception($"Could not add command: {command.Id} to the Inbox");
                }
            }

            _requests[key].RequestBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        }

        /// <summary>
        /// Awaitably adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey"></param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task" />Allows the sender to cancel the call, optional</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            Add(command, contextKey, timeoutInMilliseconds);

            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        /// Finds the command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey"></param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>ICommand.</returns>
        /// <exception cref="System.TypeLoadException"></exception>
        public T Get<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            ClearExpiredMessages();
            
            if (_requests.TryGetValue(InboxItem.CreateKey(id, contextKey), out InboxItem inboxItem))
            {
                return JsonSerializer.Deserialize<T>(inboxItem.RequestBody, JsonSerialisationOptions.Options);
            }

            throw new RequestNotFoundException<T>(id);
        }

        public bool Exists<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            ClearExpiredMessages();

            return _requests.ContainsKey(InboxItem.CreateKey(id, contextKey));
        }

        /// <summary>
        /// Checks whether a command with the specified identifier exists in the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey"></param>
        /// <param name="timeoutInMilliseconds"></param>
        /// <returns>True if it exists, False otherwise</returns>
        public Task<bool> ExistsAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Exists<T>(id, contextKey, timeoutInMilliseconds);

            tcs.SetResult(command);
            return tcs.Task;
        }

        /// <summary>
        /// Awaitably finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey"></param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task{T}" />.</returns>
        /// <returns><see cref="Task" />Allows the sender to cancel the call, optional</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<T> GetAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get<T>(id, contextKey, timeoutInMilliseconds);

            tcs.SetResult(command);
            return tcs.Task;
        }
   }
}
