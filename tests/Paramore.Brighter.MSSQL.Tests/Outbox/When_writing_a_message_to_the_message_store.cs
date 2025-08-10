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
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlOutboxWritingMessageTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private Message? _message;
        private readonly MsSqlOutbox _sqlOutbox;
        private Message? _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly Uri _dataSchema = new Uri("http://schema.example.com");
        private readonly string _subject = "TestSubject";
        private readonly CloudEventsType _type = new CloudEventsType("custom.type");
        private readonly Uri _source = new Uri("http://source.example.com");
        private readonly TraceParent _traceParent = new TraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00");
        private readonly TraceState _traceState = new TraceState("congo=t61rcWkgMzE");
        private readonly Baggage _baggage = new Baggage();
        private readonly Id _workflowId = Id.Random();
        private readonly Id _jobId = Id.Random();
        private readonly MessageHeader _messageHeader;

        public SqlOutboxWritingMessageTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            
            _baggage.LoadBaggage("userId=alice,server=node01");
            _messageHeader = new MessageHeader(
                messageId: new Id(Guid.NewGuid().ToString()),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                source: _source,
                type: _type,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                correlationId: new Id(Guid.NewGuid().ToString()),
                replyTo: new RoutingKey("ReplyAddress"),
                contentType: new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: new PartitionKey(Guid.NewGuid().ToString()),
                dataSchema: _dataSchema,
                subject: _subject,
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                traceParent: _traceParent,
                traceState: _traceState,
                baggage: _baggage,
                workflowId: _workflowId,
                jobId: _jobId
            );
            _messageHeader.Bag.Add(_key1, _value1);
            _messageHeader.Bag.Add(_key2, _value2);
            _messageHeader.Bag.Add(_key3, _value3);
            _messageHeader.Bag.Add(_key4, _value4);
            _messageHeader.Bag.Add(_key5, _value5);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_MessageStore()
        {
            _message = new Message(_messageHeader, new MessageBody("message body"));
            _sqlOutbox.Add(_message, new RequestContext());

            AssertMessage();
        }

        [Fact]
        public void When_Writing_A_Message_With_a_Null_To_The_MessageStore()
        {
            _message = new Message(_messageHeader, new MessageBody((byte[])null));
            _sqlOutbox.Add(_message, new RequestContext());

            AssertMessage();
        }

        private void AssertMessage()
        {
            _storedMessage = _sqlOutbox.Get(_message.Id, new RequestContext());

            //should read the message from the sql outbox
            Assert.Equal(_message.Body.Value, _storedMessage.Body.Value);
            //should read the header from the sql outbox
            Assert.Equal(_message.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_message.Header.MessageType, _storedMessage.Header.MessageType);
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_message.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_message.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_message.Header.ContentType, _storedMessage.Header.ContentType);
            Assert.Equal(_message.Header.PartitionKey, _storedMessage.Header.PartitionKey);

            // Additional asserts for Cloud Events and W3C Tracing properties
            Assert.Equal(_message.Header.Source, _storedMessage.Header.Source);
            Assert.Equal(_message.Header.Type, _storedMessage.Header.Type);
            Assert.Equal(_message.Header.DataSchema, _storedMessage.Header.DataSchema);
            Assert.Equal(_message.Header.Subject, _storedMessage.Header.Subject);
            Assert.Equal(_message.Header.TraceParent, _storedMessage.Header.TraceParent);
            Assert.Equal(_message.Header.TraceState, _storedMessage.Header.TraceState);
            Assert.Equal(_message.Header.Baggage, _storedMessage.Header.Baggage);
            
            //Asserts for workflow properties
            Assert.Equal(_message.Header.WorkflowId, _storedMessage.Header.WorkflowId);
            Assert.Equal(_message.Header.JobId, _storedMessage.Header.JobId);

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

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
