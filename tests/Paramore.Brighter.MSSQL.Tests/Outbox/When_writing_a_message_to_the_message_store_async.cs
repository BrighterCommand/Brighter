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
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlOutboxWritingMessageAsyncTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly Uri _dataSchema = new Uri("http://schema.example.com");
        private readonly string _subject = "TestSubject";
        private readonly CloudEventsType _type = new CloudEventsType("custom.type");
        private readonly Uri _source = new Uri("http://source.example.com");
        private readonly TraceParent _traceParent = new TraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00");
        private readonly TraceState _traceState = new TraceState("congo=t61rcWkgMzE");
        private readonly Baggage _baggage = new Baggage();
        private readonly Message _message;
        private readonly MsSqlOutbox _sqlOutbox;
        private Message? _storedMessage;
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly RequestContext _requestContext = new();
        private readonly Guid _testbagUuid = Guid.NewGuid();
        private readonly DateTime _testBagDateTime = DateTime.UtcNow;

        public SqlOutboxWritingMessageAsyncTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);

            _baggage.LoadBaggage("userId=alice,server=node01");
            var messageHeader = new MessageHeader(
                messageId:Id.Random(),
                topic: new RoutingKey("test_topic"), 
                messageType: MessageType.MT_DOCUMENT, 
                source: _source,
                type: _type,
                timeStamp: DateTime.UtcNow.AddDays(-1), 
                correlationId: Id.Random(),
                replyTo: new RoutingKey("ReplyAddress"),
                contentType: new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: new PartitionKey(Guid.NewGuid().ToString()),
                dataSchema: _dataSchema,
                subject: _subject,
                handledCount:5, 
                delayed:TimeSpan.FromMilliseconds(5),
                traceParent: _traceParent,
                traceState: _traceState,
                baggage: _baggage
            );
            messageHeader.Bag.Add(_key1, "value1");
            messageHeader.Bag.Add(_key2, "value2");
            messageHeader.Bag.Add(_key3, 123);
            messageHeader.Bag.Add(_key4, _testbagUuid);
            messageHeader.Bag.Add(_key5, _testBagDateTime);

            _message = new Message(messageHeader, new MessageBody("message body"));
            _sqlOutbox.Add(_message, _requestContext);
        }

        [Fact]
        public async Task When_Writing_A_Message_To_The_Outbox_Async()
        {
            await _sqlOutbox.AddAsync(_message, _requestContext);

            _storedMessage = await _sqlOutbox.GetAsync(_message.Id, _requestContext);
            //should read the message from the sql outbox
            Assert.Equal(_message.Body.Value, _storedMessage.Body.Value);
            //should read the header from the sql outbox
            Assert.Equal(_message.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_message.Header.MessageType, _storedMessage.Header.MessageType);
            Assert.Equal(_message.Header.TimeStamp, _storedMessage.Header.TimeStamp, TimeSpan.FromSeconds(1)); // Allow for slight differences in timestamp precision
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_message.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_message.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_message.Header.ContentType, _storedMessage.Header.ContentType);
             
            
            //Bag serialization
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key1));
            Assert.Equal("value1", _storedMessage.Header.Bag[_key1]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key2));
            Assert.Equal("value2", _storedMessage.Header.Bag[_key2]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key3));
            Assert.Equal(123, _storedMessage.Header.Bag[_key3]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key4));
            Assert.Equal(_testbagUuid, _storedMessage.Header.Bag[_key4]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key5));
            Assert.Equal(_testBagDateTime, _storedMessage.Header.Bag[_key5]);

            // Additional asserts for Cloud Events and W3C Tracing properties
            Assert.Equal(_message.Header.Source, _storedMessage.Header.Source);
            Assert.Equal(_message.Header.Type, _storedMessage.Header.Type);
            Assert.Equal(_message.Header.DataSchema, _storedMessage.Header.DataSchema);
            Assert.Equal(_message.Header.Subject, _storedMessage.Header.Subject);
            Assert.Equal(_message.Header.TraceParent, _storedMessage.Header.TraceParent);
            Assert.Equal(_message.Header.TraceState, _storedMessage.Header.TraceState);
            Assert.Equal(_message.Header.Baggage, _storedMessage.Header.Baggage);
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
