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

#if NET46
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
using EventStore.Core.Data;
using Xunit;
using Paramore.Brighter.OutBox.EventStore;


namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    [Category("EventStore")]

    public class EventStoreEmptyTests
    {
        private IList<Message> _messages;
        private Message _message1;
        private Message _message2;
        private const string StreamName = "stream-123";
        private ClusterVNode _eventStoreNode;
        private IEventStoreConnection _eventStore;
        private bool _eventStoreNodeStarted;
        private bool _eventStoreClientConnected;
        private EventStoreOutbox _eventStoreOutbox;
        private const string EmptyStreamName = "empty-123";

        [SetUp]
        public void Establish()
        {
            _eventStoreNodeStarted = false;
            _eventStoreClientConnected = false;

            var noneIp = new IPEndPoint(IPAddress.None, 0);
            _eventStoreNode = EmbeddedVNodeBuilder
                .AsSingleNode()
                .RunInMemory()
                .WithExternalTcpOn(noneIp)
                .WithInternalTcpOn(noneIp)
                .WithExternalHttpOn(noneIp)
                .WithInternalHttpOn(noneIp)
                .Build();
            _eventStoreNode.NodeStatusChanged +=
                (sender, e) =>
                {
                    if (e.NewVNodeState == VNodeState.Master) _eventStoreNodeStarted = true;
                };
            _eventStoreNode.Start();

            _eventStore = EmbeddedEventStoreConnection.Create(_eventStoreNode);
            _eventStore.Connected += (sender, e) => { _eventStoreClientConnected = true; };
            _eventStore.ConnectAsync().Wait();

            _eventStoreOutbox = new EventStoreMessageStore(_eventStore);

            _message1 = CreateMessage(0);
            _message2 = CreateMessage(1);

            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();
        }

        [TearDown]
        public void Dispose()
        {
            _eventStore.Close();
            _eventStoreNode.Stop();
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Outbox()
        {
            _messages = _eventStoreOutbox.Get(EmptyStreamName, 0, 1);

            //_returns_an_empty_list
            0.Should().Be(_messages.Count);
        }

        private Message CreateMessage(int eventNumber)
        {
            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", eventNumber);
            header.Bag.Add("streamId", StreamName);
            return new Message(header, body);
        }

        private void EnsureEventStoreNodeHasStartedAndTheClientHasConnected()
        {
            var timer = new Stopwatch();
            timer.Start();
            while (!_eventStoreNodeStarted || !_eventStoreClientConnected)
            {
                if (timer.ElapsedMilliseconds > 10000)
                {
                    throw new TimeoutException();
                }
            }
        }
    }
}

#endif
