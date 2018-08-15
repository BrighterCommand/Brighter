#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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
using Newtonsoft.Json;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    public class FakeMessageStore : IAmAMessageStore<Message>, IAmAMessageStoreAsync<Message>
    {
        private readonly List<Message> _messages = new List<Message>(); 

        public bool MessageWasAdded { get; set; }

        public void Add(Message message, int messageStoreTimeout = -1)
        {
            MessageWasAdded = true;
            _messages.Add(message);
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
            foreach (var message in _messages)
            {
                if (message.Id == messageId)
                {
                    return message;
                }
            }

            return null;
        }

        public IEnumerable<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            return _messages.Take(pageSize);
        }

        public Task AddAsync(Message message, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Add(message, messageStoreTimeout);

            return Task.FromResult(0);
        }

        public Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<Message>(cancellationToken);

            return Task.FromResult(Get(messageId, messageStoreTimeout));
        }

        public bool ContinueOnCapturedContext { get; set; }
    }

    public class DynamoDbMessage
    {
        [DynamoDBHashKey]
        public string Id { get; set; }
        [DynamoDBProperty]
        public string MessageId { get; set; }
        [DynamoDBProperty]
        public string Topic { get; set; }
        [DynamoDBProperty]
        public string MessageType { get; set; }
        [DynamoDBRangeKey]
        public string TimeStamp { get; set; }
        [DynamoDBProperty]
        public string HeaderBag { get; set; }
        [DynamoDBProperty]
        public string Body { get; set; }

        public DynamoDbMessage() { }

        public DynamoDbMessage (Message message)
        {
            Id = $"{message.Header.TimeStamp:yyyy-MM-dd}";
            MessageId = message.Id.ToString();
            Topic = message.Header.Topic;
            MessageType = message.Header.MessageType.ToString();
            TimeStamp = message.Header.TimeStamp == DateTime.MinValue ? $"{DateTime.UtcNow}" : $"{message.Header.TimeStamp}";
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
