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
using System.Collections.Concurrent;
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
        IAmAnOutboxSync<Message>,
        IAmAnOutboxAsync<Message>
    {
        private readonly DynamoDbConfiguration _configuration;
        private readonly DynamoDBContext _context;
        private readonly DynamoDBOperationConfig _dynamoOverwriteTableConfig;
        private readonly Random _random = new Random();

        private readonly ConcurrentDictionary<string, byte> _topicNames;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///  Initialises a new instance of the <see cref="DynamoDbOutbox"/> class.
        /// </summary>
        /// <param name="client">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbOutbox(IAmazonDynamoDB client, DynamoDbConfiguration configuration)
        {
            _configuration = configuration;
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

            _topicNames = new ConcurrentDictionary<string, byte>();
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

            _topicNames = new ConcurrentDictionary<string, byte>();
        }

        /// <inheritdoc />
        /// <summary>
        ///     Adds a message to the store
        /// </summary>       
        /// <param name="message">The message to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            AddAsync(message, outBoxTimeout).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <summary>
        ///     Adds a message to the store
        /// </summary>
        /// <param name="message">The message to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>        
        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var shard = GetShardNumber();
            var expiresAt = GetExpirationTime();
            var messageToStore = new MessageItem(message, shard, expiresAt);

            // Store the name of the topic as a key in a concurrent dictionary to ensure uniqueness & thread safety
            _topicNames.TryAdd(message.Header.Topic, 0);

            if (transactionConnectionProvider != null)
            {
                await AddToTransactionWrite(messageToStore, (DynamoDbUnitOfWork)transactionConnectionProvider);
            }
            else
            {
                await WriteMessageToOutbox(cancellationToken, messageToStore);
            }
        }

        /// <summary>
        ///     Returns messages that have been successfully dispatched. Eventually consistent.
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
            return DispatchedMessagesAsync(millisecondsDispatchedSince, pageSize, pageNumber, outboxTimeout, args).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Get the messages that have been dispatched
        /// </summary>
        /// <param name="hoursDispatchedSince">The number of hours since the message was dispatched</param>
        /// <param name="pageSize">The amount to return</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Messages that have already been dispatched</returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(int hoursDispatchedSince, int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            var milliseconds = TimeSpan.FromHours(hoursDispatchedSince).TotalMilliseconds;
            return await DispatchedMessagesAsync(milliseconds, pageSize, cancellationToken: cancellationToken);
        }

        /// <summary>
        ///     Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="millisecondsDispatchedSince"></param>
        /// <param name="pageSize">The number of messages to fetch.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="outboxTimeout">Timeout of sql call.</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>List of messages that need to be dispatched.</returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(double millisecondsDispatchedSince, int pageSize = 100, int pageNumber = 1,
            int outboxTimeout = -1, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            if (args == null || !args.ContainsKey("Topic"))
            {
                return await DispatchedMessagesForAllTopicsAsync(millisecondsDispatchedSince, cancellationToken);
            }

            var topic = (string)args["Topic"];
            return await DispatchedMessagesForTopicAsync(millisecondsDispatchedSince, topic, cancellationToken);
        }

        /// <inheritdoc />
        /// <summary>
        ///     Finds a command with the specified identifier.
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

        /// <inheritdoc />
        /// <summary>
        ///     Finds a message with the specified identifier.
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            return await GetMessage(messageId, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        public async Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<Message>();
            foreach (var messageId in messageIds)
            {
                messages.Add(await GetAsync(messageId, -1, cancellationToken));
            }

            return messages;
        }

        /// <summary>
        /// Get paginated list of Messages.
        /// </summary>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns>A list of messages</returns>
        public IList<Message> Get(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get paginated list of Messages.
        /// </summary>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages</returns>
        public Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            var message = await _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig, cancellationToken);
            MarkMessageDispatched(dispatchedAt ?? DateTime.UtcNow, message);

            await _context.SaveAsync(
                message, 
                _dynamoOverwriteTableConfig,
                cancellationToken);
        }

        public async Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
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
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            var message = _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig).Result;
            MarkMessageDispatched(dispatchedAt ?? DateTime.UtcNow, message);

            _context.SaveAsync(
                message, 
                _dynamoOverwriteTableConfig)
                .Wait(_configuration.Timeout);
        }

        private static void MarkMessageDispatched(DateTime? dispatchedAt, MessageItem message)
        {
            message.DeliveryTime = dispatchedAt.Value.Ticks;
            message.DeliveredAt = $"{dispatchedAt:yyyy-MM-dd}";

            // Set the outstanding created time to null to remove the attribute
            // from the item in dynamo
            message.OutstandingCreatedTime = null;
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null)
        {
            return OutstandingMessagesAsync(millisecondsDispatchedSince, pageSize, pageNumber, args).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            if (args == null || !args.ContainsKey("Topic"))
            {
                return await OutstandingMessagesForAllTopicsAsync(millisecondsDispatchedSince, cancellationToken);
            }

            var topic = args["Topic"].ToString();
            return await OutstandingMessagesForTopicAsync(millisecondsDispatchedSince, topic, cancellationToken);
        }

        public async Task<int> GetNumberOfOutstandingMessagesAsync(CancellationToken cancellationToken)
        {
            var messages = await OutstandingMessagesAsync(0, cancellationToken: cancellationToken);
            return messages.Count();
        }
        
        /// <summary>
        /// Delete messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        public void Delete(Guid[] messageIds)
        {
            DeleteAsync(messageIds, new CancellationToken()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        /// <param name="cancellationToken">Cancel an in-flight request to delete from the Outbox</param>
        public async Task DeleteAsync(Guid[] messageIds, CancellationToken cancellationToken = default)
        {
            foreach (var messageId in messageIds)
            {
                await _context.DeleteAsync<MessageItem>(messageId.ToString(), _dynamoOverwriteTableConfig, cancellationToken);
            }
        }

        private Task<TransactWriteItemsRequest> AddToTransactionWrite(MessageItem messageToStore, DynamoDbUnitOfWork dynamoDbUnitOfWork)
        {
            var tcs = new TaskCompletionSource<TransactWriteItemsRequest>();
            var attributes = _context.ToDocument(messageToStore, _dynamoOverwriteTableConfig).ToAttributeMap();
            
            var transaction = dynamoDbUnitOfWork.BeginOrGetTransaction();
            transaction.TransactItems.Add(new TransactWriteItem{Put = new Put{TableName = _configuration.TableName, Item = attributes}});
            tcs.SetResult(transaction);
            return tcs.Task;
        }
       
        private async Task<Message> GetMessage(Guid id, CancellationToken cancellationToken = default)
        {
            var messageItem = await _context.LoadAsync<MessageItem>(id.ToString(), _dynamoOverwriteTableConfig, cancellationToken);
            return messageItem?.ConvertToMessage() ?? new Message();
        }

        private async Task<IEnumerable<Message>> DispatchedMessagesForAllTopicsAsync(
            double millisecondsDispatchedSince,
            CancellationToken cancellationToken)
        {
            var sinceTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));

            // Get the list of topic names we need to query over
            var topics = _topicNames.Keys.ToList();

            // Iterate over topics until all messages are retrieved
            var messages = new List<MessageItem>();
            foreach (var topic in topics)
            {
                //in theory this is all values on that index that have a Delivered data (sparse index)
                //we just need to filter for ones in the right date range
                //As it is a GSI it can't use a consistent read
                var queryConfig = new QueryOperationConfig
                {
                    IndexName = _configuration.DeliveredIndexName,
                    KeyExpression = new KeyTopicDeliveredTimeExpression().Generate(topic, sinceTime),
                    ConsistentRead = false
                };

                messages.AddRange(await PageAllMessagesAsync(queryConfig, cancellationToken));
            }

            return messages.Select(msg => msg.ConvertToMessage());
        }

        private async Task<IEnumerable<Message>> DispatchedMessagesForTopicAsync(
           double millisecondsDispatchedSince,
           string topicName,
           CancellationToken cancellationToken)
        {
            var sinceTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));

            //in theory this is all values on that index that have a Delivered data (sparse index)
            //we just need to filter for ones in the right date range
            //As it is a GSI it can't use a consistent read
            var queryConfig = new QueryOperationConfig
            {
                IndexName = _configuration.DeliveredIndexName,
                KeyExpression = new KeyTopicDeliveredTimeExpression().Generate(topicName, sinceTime),
                ConsistentRead = false
            };

            var messages = await PageAllMessagesAsync(queryConfig, cancellationToken);
            return messages.Select(msg => msg.ConvertToMessage());
        }

        private async Task<IEnumerable<Message>> OutstandingMessagesForAllTopicsAsync(double millisecondsDispatchedSince, CancellationToken cancellationToken)
        {
            var olderThan = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));

            // Get the list of topic names we need to query over
            var topics = _topicNames.Keys.ToList();

            // Iterate over topics and their associated shards until all messages are retrieved
            var results = new List<MessageItem>();
            foreach (var topic in topics)
            {
                results.AddRange(await QueryAllOutstandingShardsAsync(topic, olderThan, cancellationToken));
            }

            return results.Select(msg => msg.ConvertToMessage());
        }

        private async Task<IEnumerable<Message>> OutstandingMessagesForTopicAsync(double millisecondsDispatchedSince,
            string topicName, CancellationToken cancellationToken)
        {
            var olderThan = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));

            var messages = (await QueryAllOutstandingShardsAsync(topicName, olderThan, cancellationToken)).ToList();
            return messages.Select(msg => msg.ConvertToMessage());
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
                var queryConfig = new QueryOperationConfig
                {
                    IndexName = _configuration.OutstandingIndexName,
                    KeyExpression = new KeyTopicOutstandingCreatedTimeExpression().Generate(topic, minimumAge, shard),
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
