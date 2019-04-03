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
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbOutbox :
        IAmAnOutbox<Message>,
        IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>,
        IAmAnOutboxViewerAsync<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<DynamoDbOutbox>);

        private readonly DynamoDBContext _context;
        private readonly DynamoDbMessageStoreConfiguration _messageStoreConfiguration;
        private readonly DynamoDBOperationConfig _operationConfig;
        private readonly DynamoDBOperationConfig _queryOperationConfig;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initialises a new instance of the <see cref="DynamoDbOutbox"/> class.
        /// </summary>
        /// <param name="context">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbOutbox(DynamoDBContext context, DynamoDbMessageStoreConfiguration configuration)
        {
            _context = context;
            _messageStoreConfiguration = configuration;

            _operationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName, 
                ConsistentRead = configuration.UseStronglyConsistentRead
            };

            _queryOperationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName, 
                IndexName = configuration.MessageIdIndex
            };
        }

        public DynamoDbOutbox(DynamoDBContext context, DynamoDbMessageStoreConfiguration configuration, DynamoDBOperationConfig queryOperationConfig)
        {
            _context = context;
            _operationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName, 
                ConsistentRead = configuration.UseStronglyConsistentRead
            };
            _queryOperationConfig = queryOperationConfig;
        }

        /// <inheritdoc />
        /// <summary>
        ///     Adds a message to the store
        /// </summary>       
        /// <param name="message">The message to be stored</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        public void Add(Message message, int outBoxTimeout = -1)
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
        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var messageToStore = new DynamoDbMessage(message);

            await _context.SaveAsync(messageToStore, _operationConfig, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// Returns messages that have been successfully dispatched
        /// </summary>
        /// <param name="millisecondsDispatchedAgo">How long ago was the message dispatched?</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(double millisecondsDispatchedAgo, int pageSize = 100, int pageNumber = 1)
        {
            //TODO: Implement dispatched messages
            throw new NotImplementedException();
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
            return GetMessageFromDynamo(messageId).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        /// <summary>
        ///     Finds a message with the specified identifier.
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await GetMessageFromDynamo(messageId, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        private async Task<Message> GetMessageFromDynamo(Guid id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var storedId = id.ToString();

            _queryOperationConfig.QueryFilter = new List<ScanCondition>
            {
                new ScanCondition(_messageStoreConfiguration.MessageIdIndex, ScanOperator.Equal, storedId)
            };

            var messages =
                await _context.QueryAsync<DynamoDbMessage>(storedId, _queryOperationConfig)
                    .GetNextSetAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

            return messages.FirstOrDefault()?.ConvertToMessage() ?? new Message();
        }

       /// <summary>
        /// Get paginated list of Messages. Not supported by DynamoDB
        /// </summary>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <returns><exception cref="NotSupportedException"></exception></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        /// <summary>
        /// Get paginated list of Messages. Not supported by DynamoDB
        /// </summary>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="cancellationToken"></param>
        /// <returns><exception cref="T:System.NotSupportedException"></exception></returns>
        public Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///     Get list of messages based on date and time
        /// </summary>
        /// <param name="topic">The topic of the message. First part of the partition key for Message Store.</param>
        /// <param name="date">The date you want to retireve messages for. Second part of the partition key for Message Store.</param>
        /// <param name="startTime">Time to retrieve messages from on given date.</param>
        /// <param name="endTime">Time to retrieve message until on given date.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="T:List Paramore.Brighter.Message"/></returns>
        public IList<Message> Get(string topic, DateTime date, DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var primaryKey = $"{topic}+{date:yyyy-MM-dd}";

            var filter = GenerateFilter(startTime, endTime);
            var query = _context.QueryAsync<DynamoDbMessage>(primaryKey, filter.Operator, filter.Values, _operationConfig)
                                .GetRemainingAsync(cancellationToken)
                                .GetAwaiter()
                                .GetResult();

            var results = query;

            return results.Select(r => r.ConvertToMessage()).ToList();            
        }
        
        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="messageId">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public Task MarkDispatchedAsync(Guid messageId, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            //TODO: Implement mark dispatched
            throw new NotImplementedException();
        }
          
        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="messageId">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid messageId, DateTime? dispatchedAt = null)
        {
            //TODO: Implement mark dispatched
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(double millSecondsSinceSent, int pageSize = 100, int pageNumber = 1)
        {
            //TODO: implement outstanding messages
            throw new NotImplementedException();
        }

        private static Filter GenerateFilter(DateTime? startTime, DateTime? endTime)
        {
            var start = $"{startTime?.Ticks ?? DateTime.MinValue.Ticks}";
            var end = $"{endTime?.Ticks ?? DateTime.MaxValue.Ticks}";
            
            return startTime is null && endTime is null || startTime.HasValue && endTime.HasValue
                ? new Filter(QueryOperator.Between, new[] { start, end })
                : startTime is null
                    ? new Filter(QueryOperator.LessThanOrEqual, new[] { end })
                    : new Filter(QueryOperator.GreaterThanOrEqual, new[] { start });
        }

        private class Filter
        {
            public QueryOperator Operator { get; }
            public IEnumerable<string> Values { get; }

            public Filter(QueryOperator @operator, IEnumerable<string> values)
                => (Operator, Values) = (@operator, values);
        }
        
    }

    public class DynamoDbMessage
    {
        [DynamoDBHashKey("Topic+Date")]
        public string TopicDate { get; set; }

        [DynamoDBRangeKey]
        public string Time { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey("MessageId")]
        public string MessageId { get; set; }

        [DynamoDBProperty]
        public string Topic { get; set; }

        [DynamoDBProperty]
        public string MessageType { get; set; }

        [DynamoDBProperty]
        public string TimeStamp { get; set; }

        [DynamoDBProperty]
        public string HeaderBag { get; set; }

        [DynamoDBProperty]
        public string Body { get; set; }

        [DynamoDBIgnore]
        public DateTime Date { get; set; }

        public DynamoDbMessage()
        {
        }

        public DynamoDbMessage(Message message)
        {
            Date = message.Header.TimeStamp == DateTime.MinValue ? DateTime.UtcNow : message.Header.TimeStamp;

            TopicDate = $"{message.Header.Topic}+{Date:yyyy-MM-dd}";
            Time = $"{Date.Ticks}";
            MessageId = message.Id.ToString();
            Topic = message.Header.Topic;
            MessageType = message.Header.MessageType.ToString();
            TimeStamp = $"{Date}";
            HeaderBag = JsonConvert.SerializeObject(message.Header.Bag);
            Body = message.Body.Value;
        }

        public Message ConvertToMessage()
        {
            var messageId = Guid.Parse(MessageId);
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType);
            var timestamp = DateTime.Parse(TimeStamp);
            var bag = JsonConvert.DeserializeObject<Dictionary<string, string>>(HeaderBag);

            var header = new MessageHeader(messageId, Topic, messageType, timestamp);

            foreach (var key in bag.Keys)
            {
                header.Bag.Add(key, bag[key]);
            }

            var body = new MessageBody(Body);

            return new Message(header, body);
        }
    }
}
