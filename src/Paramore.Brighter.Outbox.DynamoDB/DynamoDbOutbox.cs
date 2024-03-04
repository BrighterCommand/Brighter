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
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbOutbox :
        IAmAnOutboxSync<Message, TransactWriteItemsRequest>,
        IAmAnOutboxAsync<Message, TransactWriteItemsRequest>
    {
        private readonly DynamoDbConfiguration _configuration;
        private readonly DynamoDBContext _context;
        private readonly DynamoDBOperationConfig _dynamoOverwriteTableConfig;
        private readonly Random _random = new Random();
        private readonly IAmazonDynamoDB _client;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///  Initialises a new instance of the <see cref="DynamoDbOutbox"/> class.
        /// </summary>
        /// <param name="client">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbOutbox(IAmazonDynamoDB client, DynamoDbConfiguration configuration)
        {
            _configuration = configuration;
            _client = client;
            _context = new DynamoDBContext(client);
            _dynamoOverwriteTableConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = _configuration.TableName,
                ConsistentRead = true
            };

            if (_configuration.NumberOfShards > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
            }
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="DynamoDbOutbox"/> class. 
        /// </summary>
        /// <param name="context">An existing Dynamo Db Context</param>
        /// <param name="configuration">The Configuration from the context - the config is internal, so we can't grab the settings from it.</param>
        public DynamoDbOutbox(DynamoDBContext context, DynamoDbConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _dynamoOverwriteTableConfig = new DynamoDBOperationConfig { OverrideTableName = _configuration.TableName };
            
            if (_configuration.NumberOfShards > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Adds a message to the Outbox
        /// </summary>       
        /// <param name="message">The message to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        public void Add(
            Message message, 
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<TransactWriteItemsRequest> transactionProvider = null
            )
        {
            AddAsync(message, outBoxTimeout).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <summary>
        /// Adds messages to the Outbox
        /// </summary>       
        /// <param name="messages">The messages to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        public void Add(
            IEnumerable<Message> messages, int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<TransactWriteItemsRequest> transactionProvider = null
            )
        {
            foreach (var message in messages)
            {
                Add(message, outBoxTimeout, transactionProvider);
            }
        }

        /// <summary>
        /// Adds a message to the Outbox
        /// </summary>
        /// <param name="message">The message to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task AddAsync(Message message,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<TransactWriteItemsRequest> transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            var shard = GetShardNumber();
            var expiresAt = GetExpirationTime();
            var messageToStore = new MessageItem(message, shard, expiresAt);

            if (transactionProvider != null)
            {
                await AddToTransactionWrite(messageToStore, (DynamoDbUnitOfWork)transactionProvider);
            }
            else
            {
                await WriteMessageToOutbox(cancellationToken, messageToStore);
            }
        }

        /// <summary>
        /// Adds messages to the Outbox
        /// </summary>
        /// <param name="messages">The messages to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider"></param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task AddAsync(IEnumerable<Message> messages,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<TransactWriteItemsRequest> transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            foreach (var message in messages)
            {
                await AddAsync(message, outBoxTimeout, transactionProvider, cancellationToken);
            }
        }

        /// <summary>
        /// Delete messages from the Outbox
        /// </summary>
        /// <param name="messageIds"></param>
        public void Delete(Guid[] messageIds)
        {
            DeleteAsync(messageIds).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        /// <param name="cancellationToken">Should the operation be cancelled</param>
        public async Task DeleteAsync(Guid[] messageIds, CancellationToken cancellationToken = default)
        {
            foreach (var messageId in messageIds)
            {
                await _context.DeleteAsync<MessageItem>(messageId.ToString(), _dynamoOverwriteTableConfig, cancellationToken);
            }
        }
        
        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent.
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Used to pass through the topic we are searching for messages in. Use Key: "Topic"</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            int outboxTimeout = -1,
            Dictionary<string, object> args = null)
        {
            if (args == null)
            {
                throw new ArgumentException("Missing required argument", nameof(args));
            }
            
            var sinceTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));
            var topic = (string)args["Topic"];

            //in theory this is all values on that index that have a Delivered data (sparse index)
            //we just need to filter for ones in the right date range
            //As it is a GSI it can't use a consistent read
            var queryConfig = new QueryOperationConfig
            {
                IndexName = _configuration.DeliveredIndexName,
                KeyExpression = new KeyTopicDeliveredTimeExpression().Generate(topic, sinceTime),
                ConsistentRead = false
            };
           
            //block async to make this sync
            var messages = PageAllMessagesAsync(queryConfig).Result.ToList();
            return messages.Select(msg => msg.ConvertToMessage());
        }
        
        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent.
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Used to pass through the topic we are searching for messages in. Use Key: "Topic"</param>
        /// <returns>A list of dispatched messages</returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1,
            int outboxTimeout = -1, 
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default
            )
        {
            if (args == null)
            {
                throw new ArgumentException("Missing required argument", nameof(args));
            }
            
            var sinceTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));
            var topic = (string)args["Topic"];

            //in theory this is all values on that index that have a Delivered data (sparse index)
            //we just need to filter for ones in the right date range
            //As it is a GSI it can't use a consistent read
            var queryConfig = new QueryOperationConfig
            {
                IndexName = _configuration.DeliveredIndexName,
                KeyExpression = new KeyTopicDeliveredTimeExpression().Generate(topic, sinceTime),
                ConsistentRead = false
            };
           
            //block async to make this sync
            var messages = await PageAllMessagesAsync(queryConfig, cancellationToken);
            return messages.Select(msg => msg.ConvertToMessage());
        }
        
        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent. 
        /// </summary>
        /// <param name="hoursDispatchedSince">How many hours back to look</param>
        /// <param name="pageSize">The number of results to return. Only returns this number of results</param>
        /// <param name="cancellationToken">How to cancel</param>
        /// <returns></returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            int hoursDispatchedSince, 
            int pageSize = 100,
            CancellationToken cancellationToken = default
            )
        {
            var hoursToMilliseconds = TimeSpan.FromHours(hoursDispatchedSince).Milliseconds;
            return await DispatchedMessagesAsync(hoursToMilliseconds, pageSize, 1, -1, null, cancellationToken);
        }

        /// <inheritdoc />
        /// <summary>
        ///  Finds a message with the specified identifier.
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            return GetMessage(messageId)
                .ConfigureAwait(ContinueOnCapturedContext)
                .GetAwaiter()
                .GetResult();
        }
        

        /// <summary>
        /// Finds a message with the specified identifier.
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public async Task<Message> GetAsync(
            Guid messageId, 
            int outBoxTimeout = -1, 
            CancellationToken cancellationToken = default)
        {
            return await GetMessage(messageId, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args"></param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(
            Guid id, 
            DateTime? dispatchedAt = null, 
            Dictionary<string, object> args = null, 
            CancellationToken cancellationToken = default
            )
        {
            var message = await _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            MarkMessageDispatched(dispatchedAt ?? DateTime.UtcNow, message);

            await _context.SaveAsync(
                message, 
                _dynamoOverwriteTableConfig,
                cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
       }

        public async Task MarkDispatchedAsync(
            IEnumerable<Guid> ids, 
            DateTime? dispatchedAt = null, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default
            )
        {
            foreach(var messageId in ids)
            {
                await MarkDispatchedAsync(messageId, dispatchedAt, args, cancellationToken);
            }
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args"></param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            var message = _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig).Result;
            MarkMessageDispatched(dispatchedAt ?? DateTime.UtcNow, message);

            _context.SaveAsync(
                message, 
                _dynamoOverwriteTableConfig)
                .Wait(_configuration.Timeout);

        }

        private static void MarkMessageDispatched(DateTime dispatchedAt, MessageItem message)
        {
            message.DeliveryTime = dispatchedAt.Ticks;
            message.DeliveredAt = dispatchedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="args"></param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(
         double millisecondsDispatchedSince, 
         int pageSize = 100, 
         int pageNumber = 1, 
         Dictionary<string, object> args = null)
        {
            var now = DateTime.UtcNow;
            
            if (args == null)
            {
                throw new ArgumentException("Missing required argument", nameof(args));
            }

            var dispatchedTime = now.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));
            var topic = (string)args["Topic"];

            //block async to make this sync
            var messages = QueryAllOutstandingShardsAsync(topic, dispatchedTime).Result.ToList();
            return messages.Select(msg => msg.ConvertToMessage());
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="args"></param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            if (args == null)
            {
                throw new ArgumentException("Missing required argument", nameof(args));
            }

            var minimumAge = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));
            var topic = (string)args["Topic"];

            //block async to make this sync
            var messages = (await QueryAllOutstandingShardsAsync(topic, minimumAge, cancellationToken)).ToList();
            return messages.Select(msg => msg.ConvertToMessage());
        }

       private Task<TransactWriteItemsRequest> AddToTransactionWrite(MessageItem messageToStore, DynamoDbUnitOfWork dynamoDbUnitOfWork)
       {
           var tcs = new TaskCompletionSource<TransactWriteItemsRequest>();
           var attributes = _context.ToDocument(messageToStore, _dynamoOverwriteTableConfig).ToAttributeMap();
           
           var transaction = dynamoDbUnitOfWork.GetTransaction();
           transaction.TransactItems.Add(new TransactWriteItem{Put = new Put{TableName = _configuration.TableName, Item = attributes}});
           tcs.SetResult(transaction);
           return tcs.Task;
       }
       
        private async Task<Message> GetMessage(Guid id, CancellationToken cancellationToken = default)
        {
            MessageItem messageItem = await _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig, cancellationToken);
            return messageItem?.ConvertToMessage() ?? new Message();
        }
        
        private async Task<IEnumerable<MessageItem>> PageAllMessagesAsync(QueryOperationConfig queryConfig, CancellationToken cancellationToken = default)
        {
            var asyncSearch = _context.FromQueryAsync<MessageItem>(queryConfig, _dynamoOverwriteTableConfig);
            
            var messages = new List<MessageItem>();
            do
            { 
                messages.AddRange(await asyncSearch.GetNextSetAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext));
            } while (!asyncSearch.IsDone);

            return messages;
        }
        
        private async Task<IEnumerable<MessageItem>> QueryAllOutstandingShardsAsync(string topic, DateTime minimumAge, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task<IEnumerable<MessageItem>>>();

            for (int shard = 0; shard < _configuration.NumberOfShards; shard++)
            {
                // We get all the messages for topic, added within a time range
                // There should be few enough of those that we can efficiently filter for those
                // that don't have a delivery date.
                var queryConfig = new QueryOperationConfig
                {
                    IndexName = _configuration.OutstandingIndexName,
                    KeyExpression = new KeyTopicCreatedTimeExpression().Generate(topic, minimumAge, shard),
                    FilterExpression = new NoDispatchTimeExpression().Generate(),
                    ConsistentRead = false
                };

                tasks.Add(PageAllMessagesAsync(queryConfig, cancellationToken));
            }

            await Task.WhenAll(tasks);

            return tasks
                .SelectMany(x => x.Result)
                .OrderBy(x => x.CreatedAt);
        }
        
        private async Task WriteMessageToOutbox(CancellationToken cancellationToken, MessageItem messageToStore)
        {
            await _context.SaveAsync(
                    messageToStore,
                    _dynamoOverwriteTableConfig,
                    cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        private int GetShardNumber()
        {
            if (_configuration.NumberOfShards <= 0)
            {
                return 0;
            }

            return _random.Next(0, _configuration.NumberOfShards);
        }

        private long? GetExpirationTime()
        {
            if (_configuration.TimeToLive.HasValue)
            {
                return DateTimeOffset.UtcNow.Add(_configuration.TimeToLive.Value).ToUnixTimeSeconds();
            }

            return null;
        }
    }
}
