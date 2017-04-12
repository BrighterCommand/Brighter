#region Licence
/* The MIT License (MIT)
Copyright � 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using System.Linq;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
using EventStore.Core.Data;
using Xunit;
using Paramore.Brighter.MessageStore.EventStore;

namespace Paramore.Brighter.Tests.MessageStore.EventStore
{
    [Category("EventStore")]

    public class EventStoreMessageStoreTests
    {
        private IList<Message> _messages;
        private Message _message1;
        private Message _message2;
        private const string StreamName = "stream-123";
        private ClusterVNode _eventStoreNode;
        private IEventStoreConnection _eventStore;
        private bool _eventStoreNodeStarted;
        private bool _eventStoreClientConnected;
        private EventStoreMessageStore _eventStoreMessageStore;
        private const string EmptyStreamName = "empty-123";

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
                (sender, e) => { if (e.NewVNodeState == VNodeState.Master) _eventStoreNodeStarted = true; };
            _eventStoreNode.Start();

            _eventStore = EmbeddedEventStoreConnection.Create(_eventStoreNode);
            _eventStore.Connected += (sender, e) => { _eventStoreClientConnected = true; };
            _eventStore.ConnectAsync().Wait();

            _eventStoreMessageStore = new EventStoreMessageStore(_eventStore);

            _message1 = CreateMessage(0);
            _message2 = CreateMessage(1);

            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();

            _eventStoreMessageStore.Add(_message1);
            _eventStoreMessageStore.Add(_message2);
        }

        [TearDown]
        public void Dispose()
        {
            _eventStore.Close();
            _eventStoreNode.Stop();
        }

        public void When_Writing_Messages_To_The_Message_Store_Async()
        {
            _messages = _eventStoreMessageStore.Get(StreamName, 0, 2);

            //_gets_message1
           Assert.AreEqual(_messages.Count(m => MessagesEqualApartFromTimestamp(m, _message1)), 1);
            //_gets_message2
           Assert.AreEqual(_messages.Count(m => MessagesEqualApartFromTimestamp(m, _message2)), 1);

         }

        private bool MessagesEqualApartFromTimestamp(Message m1, Message m2)
        {
            var bodysAreEqual = m1.Body.Equals(m2.Body);
            var headersAreEqual = m1.Id == m2.Id
                                  && m1.Header.Topic == m2.Header.Topic
                                  && m1.Header.MessageType == m2.Header.MessageType
                                  && m1.Header.HandledCount == m2.Header.HandledCount
                                  && HeaderBagsHaveTheSameContents(m1, m2);
            return bodysAreEqual && headersAreEqual;
        }

        private bool HeaderBagsHaveTheSameContents(Message m1, Message m2)
        {
            foreach (var kvp in m1.Header.Bag)
            {
                object secondValue;
                if (!m2.Header.Bag.TryGetValue(kvp.Key, out secondValue)) return false;
                if (secondValue.ToString() != kvp.Value.ToString()) return false;
            }
            return m1.Header.Bag.Count == m2.Header.Bag.Count;
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