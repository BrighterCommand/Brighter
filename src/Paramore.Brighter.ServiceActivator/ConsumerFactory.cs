#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.ServiceActivator
{
    internal class ConsumerFactory<TRequest> : IConsumerFactory where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly ConnectionBase _connection;

        public ConsumerFactory(IAmACommandProcessor commandProcessor, IAmAMessageMapperRegistry messageMapperRegistry, ConnectionBase connection)
        {
            _commandProcessor = commandProcessor;
            _messageMapperRegistry = messageMapperRegistry;
            _connection = connection;
        }

        public Consumer Create()
        {
            switch (_connection)
            {
                case Connection c:
                    return Create(c);

                case ConnectionAsync c:
                    return Create(c);
            }

            throw new NotImplementedException();
        }

        private Consumer Create(Connection connection)
        {
            var channel = connection.ChannelFactory.CreateInputChannel(_connection.ChannelName, _connection.RoutingKey, _connection.IsDurable, highAvailability: _connection.HighAvailability);
            var messagePump = new MessagePump<TRequest>(channel, _commandProcessor, _messageMapperRegistry.Get<TRequest>())
            {
                TimeoutInMilliseconds = _connection.TimeoutInMiliseconds,
                RequeueCount = _connection.RequeueCount,
                RequeueDelayInMilliseconds = _connection.RequeueDelayInMilliseconds,
                UnacceptableMessageLimit = _connection.UnacceptableMessageLimit
            };

            return new Consumer(_connection.Name, channel, messagePump);
        }

        private Consumer Create(ConnectionAsync connection)
        {
            var channel = connection.ChannelFactory.CreateInputChannel(_connection.ChannelName, _connection.RoutingKey, _connection.IsDurable, highAvailability: _connection.HighAvailability);
            var messagePump = new MessagePumpAsync<TRequest>(channel, _commandProcessor, _messageMapperRegistry.Get<TRequest>())
            {
                TimeoutInMilliseconds = _connection.TimeoutInMiliseconds,
                RequeueCount = _connection.RequeueCount,
                RequeueDelayInMilliseconds = _connection.RequeueDelayInMilliseconds,
                UnacceptableMessageLimit = _connection.UnacceptableMessageLimit
            };

            return new Consumer(_connection.Name, channel, messagePump);
        }
    }
}