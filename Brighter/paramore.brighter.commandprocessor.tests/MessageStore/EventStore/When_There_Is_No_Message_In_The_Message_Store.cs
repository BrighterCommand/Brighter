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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
using EventStore.Core.Data;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.eventstore;

namespace paramore.commandprocessor.tests.MessageStore.EventStore
{
    [Tags("Requires", new[] {"Waiting on EventStore release"})]
    [Subject(typeof(EventStoreMessageStore))]
    public class When_There_Is_No_Message_In_The_Message_Store
    {
        private static IList<Message> s_messages;
        private static Message s_message1;
        private static Message s_message2;
        private const string StreamName = "stream-123";
        private static ClusterVNode s_eventStoreNode;
        private static IEventStoreConnection s_eventStore;
        private static bool s_eventStoreNodeStarted;
        private static bool s_eventStoreClientConnected;
        private static EventStoreMessageStore s_eventStoreMessageStore;
        private const string EmptyStreamName = "empty-123";

        private Establish _context = () =>
        {
            s_eventStoreNodeStarted = false;
            s_eventStoreClientConnected = false;

            var noneIp = new IPEndPoint(IPAddress.None, 0);
            s_eventStoreNode = EmbeddedVNodeBuilder
                .AsSingleNode()
                .RunInMemory()
                .WithExternalTcpOn(noneIp)
                .WithInternalTcpOn(noneIp)
                .WithExternalHttpOn(noneIp)
                .WithInternalHttpOn(noneIp)
                .Build();
            s_eventStoreNode.NodeStatusChanged +=
                (sender, e) => { if (e.NewVNodeState == VNodeState.Master) s_eventStoreNodeStarted = true; };
            s_eventStoreNode.Start();

            s_eventStore = EmbeddedEventStoreConnection.Create(s_eventStoreNode);
            s_eventStore.Connected += (sender, e) => { s_eventStoreClientConnected = true; };
            s_eventStore.ConnectAsync().Wait();

            s_eventStoreMessageStore = new EventStoreMessageStore(s_eventStore, new LogProvider.NoOpLogger());

            s_message1 = CreateMessage(0);
            s_message2 = CreateMessage(1);

            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();
        };

        private Cleanup _stop_event_store = () =>
        {
            s_eventStore.Close();
            s_eventStoreNode.Stop();
        };

        private Because _of = () => s_messages = s_eventStoreMessageStore.Get(EmptyStreamName, 0, 1);

        private It _returns_an_empty_list = () => s_messages.Count.ShouldEqual(0);

        private static Message CreateMessage(int eventNumber)
        {
            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", eventNumber);
            header.Bag.Add("streamId", StreamName);
            return new Message(header, body);
        }

        private static void EnsureEventStoreNodeHasStartedAndTheClientHasConnected()
        {
            var timer = new Stopwatch();
            timer.Start();
            while (!s_eventStoreNodeStarted || !s_eventStoreClientConnected)
            {
                if (timer.ElapsedMilliseconds > 10000)
                {
                    throw new TimeoutException();
                }
            }
        }


    }
}