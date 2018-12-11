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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ;
using RabbitMQ.Client;

namespace Paramore.Brighter.Tests.MessagingGateway.RMQ
{

    internal class QueueFactory
    {
        private readonly RmqMessagingGatewayConnection _connection;
        private readonly string _channelName;
        private readonly string[] _routingKeys;

        public QueueFactory(RmqMessagingGatewayConnection connection, string channelName, params string[] routingKeys)
        {
            _connection = connection;
            _channelName = channelName;
            _routingKeys = routingKeys;
        }

        public void Create(int timeToDelayForCreationInMilliseconds)
        {
            var connectionFactory = new ConnectionFactory {Uri = _connection.AmpqUri.Uri};
            using (var connection = connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.DeclareExchangeForConnection(_connection);
                    channel.QueueDeclare(_channelName, false, false, false, null);
                    if (_routingKeys.Any())
                    {
                        foreach (var routingKey in _routingKeys)
                            channel.QueueBind(_channelName, _connection.Exchange.Name, routingKey);
                    }
                    else
                    {
                        channel.QueueBind(_channelName, _connection.Exchange.Name, _channelName);
                    }

                }
            }

            //We need to delay to actually create these queues before we send to them
            Task.Delay(timeToDelayForCreationInMilliseconds).Wait();
        }
    }
}    

