#region Licence
/* The MIT License (MIT)
Copyright © 2019 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Diagnostics;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    public class EventStoreFixture : IAsyncLifetime
    {
        protected IEventStoreConnection Connection { get; private set; }
        protected bool EventStoreClientConnected { get; private set; }
        
        protected string StreamName { get; private set; }
        
        public async Task InitializeAsync()
        {
            var endpoint = new Uri("tcp://127.0.0.1:1113");
            var settings = ConnectionSettings.Create().KeepReconnecting().KeepRetrying().SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
            var connectionName = $"M={Environment.MachineName},P={Process.GetCurrentProcess().Id},T={DateTimeOffset.UtcNow.Ticks}";

            Connection = EventStoreConnection.Create(settings, endpoint, connectionName);
            Connection.Connected += (sender, e) =>
            {
                EventStoreClientConnected = true;
            };

            await Connection.ConnectAsync();

            StreamName = $"{Guid.NewGuid()}";
        }
        
        protected void EnsureEventStoreNodeHasStartedAndTheClientHasConnected()
        {
            var timer = new Stopwatch();
            timer.Start();
            
            while (!EventStoreClientConnected)
            {
                if (timer.ElapsedMilliseconds > 10000)
                {
                    throw new TimeoutException();
                }
            }
        }
        
        protected bool MessagesEqualApartFromTimestamp(Message m1, Message m2)
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
                if (!m2.Header.Bag.TryGetValue(kvp.Key, out object secondValue)) return false;
                if (secondValue.ToString() != kvp.Value.ToString()) return false;
            }
            return m1.Header.Bag.Count == m2.Header.Bag.Count;
        }
        
        protected Message CreateMessage(int eventNumber, string streamName)
        {
            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(Guid.NewGuid(), "Topic", MessageType.MT_EVENT);
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", eventNumber);
            header.Bag.Add("streamId", streamName);
            return new Message(header, body);
        }

        public async Task DisposeAsync()
        {
            Connection.Close();
        }
    }
}
