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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class InMemoryCommandStore.
    /// This is mainly intended to support developer tests where a persistent command store is not needed
    /// </summary>
    public class InMemoryCommandStore : IAmACommandStore, IAmACommandStoreAsync
    {
        private readonly Dictionary<Guid, CommandStoreItem> _commands = new Dictionary<Guid, CommandStoreItem>();

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
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        public void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            if (!_commands.ContainsKey(command.Id))
            {
                _commands.Add(command.Id, new CommandStoreItem(typeof (T), string.Empty));
            }

            _commands[command.Id].CommandBody = JsonConvert.SerializeObject(command);
        }

        /// <summary>
        /// Awaitably adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task" />Allows the sender to cancel the call, optional</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task AddAsync<T>(T command, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<object>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            Add(command, timeoutInMilliseconds);

            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        /// Finds the command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>ICommand.</returns>
        /// <exception cref="System.TypeLoadException"></exception>
        public T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T : class, IRequest, new()
        {
            if (!_commands.ContainsKey(id))
                return null;

            var commandStoreItem = _commands[id];
            if (commandStoreItem.CommandType != typeof (T))
                throw new TypeLoadException(string.Format("The type of item {0) is {1} not{2}", id,
                    commandStoreItem.CommandType.Name, typeof (T).Name));

            return JsonConvert.DeserializeObject<T>(commandStoreItem.CommandBody);
        }

        /// <summary>
        /// Awaitably finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="Task{T}" />.</returns>
        /// <returns><see cref="Task" />Allows the sender to cancel the call, optional</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<T> GetAsync<T>(Guid id, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest, new()
        {
            var tcs = new TaskCompletionSource<T>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var command = Get<T>(id, timeoutInMilliseconds);

            tcs.SetResult(command);
            return tcs.Task;
        }

        /// <summary>
        /// Class CommandStoreItem.
        /// </summary>
        private class CommandStoreItem
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CommandStoreItem"/> class.
            /// </summary>
            /// <param name="commandType">Type of the command.</param>
            /// <param name="commandBody">The command body.</param>
            /// <param name="commandWhen">The command when.</param>
            private CommandStoreItem(Type commandType, string commandBody, DateTime commandWhen)
            {
                CommandType = commandType;
                CommandBody = commandBody;
                CommandWhen = commandWhen;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="CommandStoreItem"/> class.
            /// </summary>
            /// <param name="commandType">Type of the command.</param>
            /// <param name="commandBody">The command body.</param>
            public CommandStoreItem(Type commandType, string commandBody)
                : this(commandType, commandBody, DateTime.UtcNow) {}

            /// <summary>
            /// Gets or sets the command body.
            /// </summary>
            /// <value>The command body.</value>
            public string CommandBody { get; set; }
            
            /// <summary>
            /// Gets or sets the type of the command.
            /// </summary>
            /// <value>The type of the command.</value>
            public Type CommandType { get; private set; }

            /// <summary>
            /// Gets or sets the command when.
            /// </summary>
            /// <value>The command when.</value>
            public DateTime CommandWhen { get; private set; }
        }
    }
}