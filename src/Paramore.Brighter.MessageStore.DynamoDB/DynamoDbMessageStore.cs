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

namespace Paramore.Brighter.MessageStore.DynamoDB
{
    public class DynamoDbMessageStore :
        IAmAMessageStore<Message>,
        IAmAMessageStoreAsync<Message>,
        IAmAMessageStoreViewer<Message>,
        IAmAMessageStoreViewerAsync<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<DynamoDbMessageStore>);

        private readonly DynamoDBContext _context;
        private readonly DynamoDbStoreConfiguration _storeConfiguration;
        private readonly DynamoDBOperationConfig _operationConfig;
        private readonly DynamoDBOperationConfig _queryOperationConfig;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initialises a new instance of the <see cref="DynamoDbMessageStore"/> class.
        /// </summary>
        /// <param name="context">The DynamoDBContext</param>
        /// <param name="tableName">The table name to store messages</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbMessageStore(DynamoDBContext context, DynamoDbStoreConfiguration configuration)
        {
            _context = context;
            _storeConfiguration = configuration;

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

        public DynamoDbMessageStore(DynamoDBContext context, DynamoDbStoreConfiguration configuration, DynamoDBOperationConfig queryOperationConfig)
        {
            _context = context;
            _operationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName,
                ConsistentRead = configuration.UseStronglyConsistentRead
            };
            _queryOperationConfig = queryOperationConfig;
        }

        /// <summary>
        ///     Adds a message to the store
        /// </summary>       
        /// <param name="message">The message to be stored</param>
        /// <param name="messageStoreTimeout">Timeout in milliseconds; -1 for default timeout</param>
        public void Add(Message message, int messageStoreTimeout = -1)
        {
            AddAsync(message, messageStoreTimeout).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }        

        /// <summary>
        ///     Adds a message to the store
        /// </summary>
        /// <param name="message">The message to be stored</param>
        /// <param name="messageStoreTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>        
        public async Task AddAsync(Message message, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var messageToStore = new DynamoDbMessage(message);

            await _context.SaveAsync(messageToStore, _operationConfig, cancellationToken)
                          .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        ///     Finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see cref="Message"/></returns>
        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            return GetMessageFromDynamo(messageId).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        public async Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await GetMessageFromDynamo(messageId, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        private async Task<Message> GetMessageFromDynamo(Guid id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var storedId = id.ToString();

            _queryOperationConfig.QueryFilter = new List<ScanCondition>
            {
                new ScanCondition(_storeConfiguration.MessageIdIndex, ScanOperator.Equal, storedId)
            };

            var messages = 
                await _context.QueryAsync<DynamoDbMessage>(storedId, _queryOperationConfig)
                                .GetNextSetAsync(cancellationToken)
                                .ConfigureAwait(ContinueOnCapturedContext);            

            return messages.FirstOrDefault()?.ConvertToMessage() ?? new Message();
        }
        
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            throw new NotSupportedException();
        }

        public Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotSupportedException();
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

        public DynamoDbMessage() { }

        public DynamoDbMessage (Message message)
        {            
            Date = message.Header.TimeStamp == DateTime.MinValue ? DateTime.UtcNow : message.Header.TimeStamp;

            TopicDate = $"{message.Header.Topic}+{Date:yyyy-MM-dd}";
            Time = $"{Date:T}";
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
