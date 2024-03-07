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
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Paramore.Brighter.EventStore.Tests.Outbox
{
    public enum EventStoreState
    {
        Initializing,
        Connected,
        Closed,
        Failed
    }
    
    [CollectionDefinition("EventStore")]
    public class DatabaseCollection : ICollectionFixture<EventStoreFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
    
    public class EventStoreFixture : IDisposable
    {
        private EventStoreState _eventStoreConnectionStatus;
        private IEventStoreConnection _connection;
        private readonly IMessageSink _output;
        private readonly int _timeout;
        
        public IEventStoreConnection Connection { get; }

        public EventStoreFixture(IMessageSink output)
        {
            _output = output;
            _timeout = 120000;

            //we need to do sync over async in a constructor - we don't have sync EventStore APIs
            Connection = ConnectAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _connection?.Close();
        }

        private async Task<IEventStoreConnection> ConnectAsync()
        {
            var endpoint = new Uri("tcp://127.0.0.1:1113");
            var settings = ConnectionSettings
                .Create()
                .KeepReconnecting()
                .KeepRetrying()
                .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
                .UseConsoleLogger();
            var connectionName = $"M={Environment.MachineName},P={Process.GetCurrentProcess().Id},T={DateTimeOffset.UtcNow.Ticks}";

            _connection = EventStoreConnection.Create(settings, endpoint, connectionName);
            _connection.Connected += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Connected;
            };
            _connection.Disconnected += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Failed;
                var message = new DiagnosticMessage("EventStore disconnected");
                _output.OnMessage(message);
            };
            _connection.Reconnecting += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Initializing;
                var message = new DiagnosticMessage("EventStore reconnecting");
                _output.OnMessage(message);
            };
            _connection.AuthenticationFailed += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Failed;
                var message = new DiagnosticMessage("EventStore authentication failed", e.Reason);
                _output.OnMessage(message);
            };
            _connection.ErrorOccurred += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Failed;
                var message = new DiagnosticMessage("EventStore connection error", e.Exception);
                _output.OnMessage(message);
            };
            _connection.Closed += (sender, e) =>
            {
                _eventStoreConnectionStatus = EventStoreState.Failed;
                var message = new DiagnosticMessage("EventStore connection closed");
                _output.OnMessage(message);
            };

            await _connection.ConnectAsync();
            
            EnsureEventStoreNodeHasStartedAndTheClientHasConnected();

            return _connection;
        }

        private void EnsureEventStoreNodeHasStartedAndTheClientHasConnected()
        {
            var timer = new Stopwatch();
            timer.Start();
            
            while (_eventStoreConnectionStatus  == EventStoreState.Initializing)
                if (timer.ElapsedMilliseconds > _timeout)
                    break;
                else
                    Task.Delay(100).Wait();
            
            
            timer.Stop();
            
            switch (_eventStoreConnectionStatus)
            {
                case EventStoreState.Failed:
                    throw new Exception("EventStore connection failed, see console output for details");
                case EventStoreState.Initializing:
                    throw new Exception("EventStore connection timed out");
            }
            
        }
    }
}
