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

namespace Paramore.Brighter.EventStore.Tests.Outbox
{
    public class EventStoreFixture : IAsyncLifetime
    {
        protected IEventStoreConnection Connection { get; private set; }
        protected bool EventStoreClientConnected { get; private set; }

        protected string StreamName { get; private set; }

        public async Task InitializeAsync()
        {
            var endpoint = new Uri("tcp://127.0.0.1:1113");
            var settings = ConnectionSettings
                .Create()
                .KeepReconnecting()
                .KeepRetrying()
                .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
                .UseConsoleLogger();
            var connectionName = $"M={Environment.MachineName},P={Process.GetCurrentProcess().Id},T={DateTimeOffset.UtcNow.Ticks}";

            Connection = EventStoreConnection.Create(settings, endpoint, connectionName);
            Connection.Connected += (sender, e) =>
            {
                EventStoreClientConnected = true;
            };

            await Connection.ConnectAsync();

            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();

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

        public async Task DisposeAsync()
        {
            Connection.Close();
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }
    }
}
