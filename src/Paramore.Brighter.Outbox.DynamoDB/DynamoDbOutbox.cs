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
        
        private readonly DynamoDbConfiguration _configuration;
        private readonly DynamoDBContext _context;
        private AmazonDynamoDBClient _client;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initialises a new instance of the <see cref="DynamoDbOutbox"/> class.
        /// </summary>
        /// <param name="client">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbOutbox(DynamoDbConfiguration configuration)
        {
            _configuration = configuration;
            _client = new AmazonDynamoDBClient(configuration.Credentials, configuration.Region);
            _context = new DynamoDBContext(_client); 
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

            await _context.SaveAsync(
                    messageToStore, 
                    new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName, ConsistentRead = _configuration.UseStronglyConsistentRead}, 
                    cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// Returns messages that have been successfully dispatched
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(double millisecondsDispatchedSince, int pageSize = 100, int pageNumber = 1, int outboxTimeout = -1)
        {
            var sinceTime = DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(millisecondsDispatchedSince));

            var queryConfig = new QueryOperationConfig
            {
                IndexName = _configuration.DeliveredIndexName,
                KeyExpression = GenerateTimeSinceExpression(sinceTime),
                ConsistentRead = true
            };
           
            //in theory this is all values on that index that have a Delivered data (sparse index) starting at
            //a value, but what we actually need is all values in a date range on global secondary.

            var messages = _context.FromQueryAsync<DynamoDbMessage>(
                    queryConfig, 
                    new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName})
               .GetNextSetAsync()
               .ConfigureAwait(ContinueOnCapturedContext)
               .GetAwaiter()
               .GetResult();

            return messages.Select(msg => msg.ConvertToMessage());
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
            return GetMessage(messageId).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
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
            return await GetMessage(messageId, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
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

            var filter = GenerateTimeRangeFilter(startTime, endTime);
            var query = _context.QueryAsync<DynamoDbMessage>(
                                    primaryKey, 
                                    filter.Operator, 
                                    filter.Values, 
                                    new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName, ConsistentRead = _configuration.UseStronglyConsistentRead})
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
        public async Task MarkDispatchedAsync(Guid messageId, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var message = await GetDynamoDbMessage(messageId);
            message.MarkMessageDelivered(dispatchedAt.HasValue ? dispatchedAt.Value : DateTime.UtcNow);

            await _context.SaveAsync(
                message, 
                new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName, ConsistentRead = _configuration.UseStronglyConsistentRead},
                cancellationToken);
       }
          
        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="messageId">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid messageId, DateTime? dispatchedAt = null)
        {
            var message = GetDynamoDbMessage(messageId).Result;
            message.DeliveryTime = $"{dispatchedAt:yyyy-MM-dd}";

            _context.SaveAsync(
                message, 
                new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName, ConsistentRead = _configuration.UseStronglyConsistentRead})
                .Wait(_configuration.Timeout);

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
            /*
            var primaryKey = $"{topic}+{date:yyyy-MM-dd}";

            var filter = GenerateTimeRangeFilter(startTime, endTime);
            var query = _context.QueryAsync<DynamoDbMessage>(
                                    primaryKey, 
                                    filter.Operator, 
                                    filter.Values, 
                                    new DynamoDBOperationConfig{OverrideTableName = _configuration.TableName, ConsistentRead = _configuration.UseStronglyConsistentRead})
                                .GetRemainingAsync(cancellationToken)
                                .GetAwaiter()
                                .GetResult();

            var results = query;

            return results.Select(r => r.ConvertToMessage()).ToList();     
            */
            return null;
        }
        
        private async Task<DynamoDbMessage> GetDynamoDbMessage(Guid id, CancellationToken cancellationToken = default(CancellationToken))
        {
           var operationConfig = new DynamoDBOperationConfig
           {
               OverrideTableName = _configuration.TableName, 
               IndexName = _configuration.MessageIdIndexName,
               ConsistentRead = true
           };

           var messages = await _context.QueryAsync<DynamoDbMessage>(id.ToString(), operationConfig)
               .GetNextSetAsync(cancellationToken)
               .ConfigureAwait(ContinueOnCapturedContext);

           return messages.FirstOrDefault();
        }

        private async Task<Message> GetMessage(Guid id, CancellationToken cancellationToken = default(CancellationToken))
        {
            DynamoDbMessage dynamoDbMessage = await GetDynamoDbMessage(id, cancellationToken);
            return dynamoDbMessage?.ConvertToMessage() ?? new Message();
        }

        private static Expression GenerateTimeSinceExpression(DateTime sinceTime)
        {
            var expression = new Expression();
            expression.ExpressionStatement = "DeliveryTime >= :v_SinceTime";
            
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_SinceTime", sinceTime.Ticks);

            expression.ExpressionAttributeValues = values;

            return expression;
        }

        private static Filter GenerateTimeRangeFilter(DateTime? startTime, DateTime? endTime)
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
}
