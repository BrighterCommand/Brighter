#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
using System.Net.Mime;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlOutboxWritingMessageTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly Message _messageEarliest;
        private readonly PostgreSqlOutbox _sqlOutbox;
        private Message? _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
        private readonly RequestContext _context;
        private readonly Id _workflowId = Id.Random();
        private readonly Id _jobId = Id.Random();

        public SqlOutboxWritingMessageTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper();
            _postgresSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
            var messageHeader = new MessageHeader(
                messageId:    Id.Random(),
                topic:        new RoutingKey("test_topic"),
                messageType:  MessageType.MT_DOCUMENT,
                source:       new Uri("https://source.test"),
                type:         new CloudEventsType("test_event_type"),
                timeStamp:    DateTime.UtcNow.AddDays(-1),
                correlationId:Id.Random(),
                replyTo:      new RoutingKey("ReplyTo"),
                contentType:  new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: Guid.NewGuid().ToString(),
                dataSchema:   new Uri("https://schema.test"),
                subject:      "testSubject",
                handledCount: 5,
                delayed:      TimeSpan.FromMilliseconds(5),
                traceParent:  "00-abcdef0123456789-abcdef0123456789-01",
                traceState:   "state123",
                baggage:      new Baggage(),
                workflowId: _workflowId,
                jobId: _jobId);
            
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);
            _context = new RequestContext();

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
            _sqlOutbox.Add(_messageEarliest, _context);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_PostgreSql_Outbox()
        {
            _storedMessage = _sqlOutbox.Get(_messageEarliest.Id,_context);

            //should read the message from the sql outbox
            Assert.Equal(_messageEarliest.Body.Value, _storedMessage.Body.Value);
            //should read the header from the sql outbox
            Assert.Equal(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
            Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_messageEarliest.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_messageEarliest.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_messageEarliest.Header.ContentType, _storedMessage.Header.ContentType);
            Assert.Equal(_messageEarliest.Header.PartitionKey, _storedMessage.Header.PartitionKey); 
            
            //Bag serialization
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key1));
            Assert.Equal(_value1, _storedMessage.Header.Bag[_key1]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key2));
            Assert.Equal(_value2, _storedMessage.Header.Bag[_key2]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key3));
            Assert.Equal(_value3, _storedMessage.Header.Bag[_key3]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key4));
            Assert.Equal(_value4, _storedMessage.Header.Bag[_key4]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key5));
            Assert.Equal(_value5, _storedMessage.Header.Bag[_key5]);
            
            //Asserts for workflow properties
            Assert.Equal(_messageEarliest.Header.WorkflowId, _storedMessage.Header.WorkflowId);
            Assert.Equal(_messageEarliest.Header.JobId, _storedMessage.Header.JobId);

            // new fields assertions
            Assert.Equal(_messageEarliest.Header.Source,       _storedMessage.Header.Source);
            Assert.Equal(_messageEarliest.Header.Type,         _storedMessage.Header.Type);
            Assert.Equal(_messageEarliest.Header.DataSchema,   _storedMessage.Header.DataSchema);
            Assert.Equal(_messageEarliest.Header.Subject,      _storedMessage.Header.Subject);
            Assert.Equal(_messageEarliest.Header.TraceParent,  _storedMessage.Header.TraceParent);
            Assert.Equal(_messageEarliest.Header.TraceState,   _storedMessage.Header.TraceState);
        }

        public void Dispose()
        {
            _postgresSqlTestHelper.CleanUpDb();
        }
    }
}
