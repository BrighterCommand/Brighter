#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Inbox.DynamoDB
{
    public class DynamoDbInbox : IAmAnInbox, IAmAnInboxAsync
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<DynamoDbInbox>);
       
        private readonly DynamoDBContext _context;

       public bool ContinueOnCapturedContext { get; set; }
       
        /// <summary>
        ///     Initialises a new instance of the <see cref="DynamoDbInbox"/> class.
        /// </summary>
        /// <param name="context">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        /// <param name="client">The Amazon Dynamo Db client to use</param>
        public DynamoDbInbox(IAmazonDynamoDB client )
        {
            _context = new DynamoDBContext(client);
        }

        /// <summary>
        ///     Adds a command to the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command to be stored</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {            
            AddAsync(command, contextKey)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        ///     Finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see cref="T"/></returns>
        public T Get<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            return GetCommandAsync<T>(id, contextKey)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Adds a command to the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command to be stored</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>D
        public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            await _context
                .SaveAsync(new CommandItem<T>(command, contextKey), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Finds the command based on the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
        /// <returns><see cref="Task{T}"/></returns>
        public async Task<T> GetAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {                
            return await GetCommandAsync<T>(id, contextKey, cancellationToken).ConfigureAwait(false);
        }

       /// <summary>
        /// Checks if the command exists based on the id
        /// </summary>
        /// <param name="id">The identifier</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
        /// <typeparam name="T">Type of command being checked</typeparam>
        /// <returns><see cref="bool.True"/> if Commadn exists, otherwise <see cref="bool.False"/></returns>
        public async Task<bool> ExistsAsync<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
       {
           try
           {
               var command = await GetCommandAsync<T>(id, contextKey, cancellationToken).ConfigureAwait(false);
               return command !=null;
           }
           catch (RequestNotFoundException<T>)
           {
               return false;
           }
       }


       /// <summary>
        ///     Checks if the command exists based on the id
        /// </summary>
        /// <param name="id">The identifier</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>        
        /// <typeparam name="T">Type of command being checked</typeparam>
        /// <returns><see cref="bool.True"/> if Commadn exists, otherwise <see cref="bool.False"/></returns>
        public bool Exists<T>(Guid id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            return ExistsAsync<T>(id, contextKey).Result;
        }

        private async Task<T> GetCommandAsync<T>(Guid id, string contextKey, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var queryConfig = new QueryOperationConfig
            {
                KeyExpression = new KeyIdContextExpression().Generate(id, contextKey),
                ConsistentRead = true
            };
           
            //block async to make this sync
            var messages = await PageAllMessagesAsync<T>(queryConfig).ConfigureAwait(false);

            var result = messages.Select(msg => msg.ConvertToCommand()).FirstOrDefault();
            if ( result == null)
                throw new RequestNotFoundException<T>(id);

            return result;
        }

        private async Task<IEnumerable<CommandItem<T>>> PageAllMessagesAsync<T>(QueryOperationConfig queryConfig) 
            where T: class, IRequest 
        {
            var asyncSearch = _context.FromQueryAsync<CommandItem<T>>(queryConfig);
            
            var messages = new List<CommandItem<T>>();
            do
            {
              messages.AddRange(await asyncSearch.GetNextSetAsync().ConfigureAwait(false));
            } while (!asyncSearch.IsDone);

            return messages;
        }
 
        
   }
}
