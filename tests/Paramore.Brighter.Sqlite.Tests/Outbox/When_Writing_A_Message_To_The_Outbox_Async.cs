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
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox
{
    [Trait("Category", "Sqlite")]
    public class SqliteOutboxWritingMessageAsyncTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteOutbox _sqlOutbox;
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly string _value1 = "_value1";
        private readonly string _value2 = "_value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        
        private readonly Uri _source;
        private readonly CloudEventsType _type;
        private readonly Uri _dataSchema;
        private readonly string _subject;
        private readonly TraceParent _traceParent;
        private readonly TraceState _traceState;
        private readonly Baggage _baggage;
        private readonly Id _workflowId;
        private readonly Id _jobId;
        
        private readonly Message _messageEarliest;
        private Message _storedMessage;
 
        public SqliteOutboxWritingMessageAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sqlOutbox = new SqliteOutbox(_sqliteTestHelper.OutboxConfiguration);
 
            // define and assign the extra header properties
            _source = new Uri("http://example.org/source");
            _type = new CloudEventsType("custom-message-type");
            _dataSchema = new Uri("http://example.org/schema");
            _subject = "custom-subject";
            _traceParent = new TraceParent("00-abcdef1234567890abcdef1234567890-abcdef1234567890-01");
            _traceState = new TraceState("state");
            _baggage = Baggage.FromString("userId=123,sessionId=xyz");
            _workflowId = Id.Random();
            _jobId = Id.Random();

            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("ReplyTo"),
                contentType: new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: Guid.NewGuid().ToString(),
                source: _source,
                type: _type,
                dataSchema: _dataSchema,
                subject: _subject,
                traceParent: _traceParent,
                traceState: _traceState,
                baggage: _baggage,
                workflowId: _workflowId,
                jobId: _jobId
            );
            
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
        }

        [Fact]
        public async Task When_Writing_A_Message_To_The_Outbox_Async()
        {
            await _sqlOutbox.AddAsync(_messageEarliest, new RequestContext());

            _storedMessage = await _sqlOutbox.GetAsync(_messageEarliest.Id, new RequestContext());

            //should read the message from the sql outbox
            Assert.Equal(_messageEarliest.Body.Value, _storedMessage.Body.Value);
            //should read the message header first bag item from the sql outbox
            //should read the message header timestamp from the sql outbox
            Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
            //should read the message header topic from the sql outbox =
            Assert.Equal(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
            //should read the message header type from the sql outbox
            Assert.Equal(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
            
            
            //Asserts for workflow properties
            Assert.Equal(_messageEarliest.Header.WorkflowId, _storedMessage.Header.WorkflowId);
            Assert.Equal(_messageEarliest.Header.JobId, _storedMessage.Header.JobId);
            
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
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
