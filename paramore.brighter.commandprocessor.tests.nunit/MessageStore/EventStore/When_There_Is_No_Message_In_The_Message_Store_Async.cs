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
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.eventstore;
using NUnit.Specifications;
using nUnitShouldAdapter;
using NUnit.Framework;

namespace paramore.commandprocessor.tests.MessageStore.EventStore
{
    [Category("EventStore")]
    [TestFixture]
    public class EventStoreEmptyAsyncTests
    {
        private static IList<Message> s_messages;
        private const string StreamName = "stream-123";
        private static ClusterVNode s_eventStoreNode;
        private static IEventStoreConnection s_eventStore;
        private static bool s_eventStoreNodeStarted;
        private static bool s_eventStoreClientConnected;
        private static EventStoreMessageStore s_eventStoreMessageStore;
        private const string EmptyStreamName = "empty-123";

        [SetUp]
        public void Establish()
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

            s_eventStoreMessageStore = new EventStoreMessageStore(s_eventStore);

            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();
        }

        [TearDown]
        public void Cleanup()
        {
            s_eventStore.Close();
            s_eventStoreNode.Stop();
        }

        [Test]
        public void When_There_Is_No_Message_In_The_Message_Store()
        {
            AsyncContext.Run(async () => s_messages = await s_eventStoreMessageStore.GetAsync(EmptyStreamName, 0, 1));

            //_returns_an_empty_list
            s_messages.Count.ShouldEqual(0);
        }

        private void EnsureEventStoreNodeHasStartedAndTheClientHasConnected()
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
#endif
