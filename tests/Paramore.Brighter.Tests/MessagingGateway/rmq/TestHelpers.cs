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
using Paramore.Brighter.MessagingGateway.RMQ.MessagingGatewayConfiguration;
using RabbitMQ.Client;

namespace Paramore.Brighter.Tests.MessagingGateway.RMQ
{
    internal class TestRMQListener
    {
        private readonly string _channelName;
        private readonly ConnectionFactory _connectionFactory;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public TestRMQListener(RmqMessagingGatewayConnection connection, string channelName)
        {
            _channelName = channelName;
            _connectionFactory = new ConnectionFactory {Uri = connection.AmpqUri.Uri.ToString()};
            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.DeclareExchangeForConnection(connection);
            _channel.QueueDeclare(_channelName, false, false, false, null);
            _channel.QueueBind(_channelName, connection.Exchange.Name, _channelName);
        }

        public TestRMQListener(RmqMessagingGatewayConnection connection, string channelName, params string[] routingKeys)
        {
            _channelName = channelName;
            _connectionFactory = new ConnectionFactory { Uri = connection.AmpqUri.Uri.ToString() };
            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.DeclareExchangeForConnection(connection);
            _channel.QueueDeclare(_channelName, false, false, false, null);

            foreach (var routingKey in routingKeys)
                _channel.QueueBind(_channelName, connection.Exchange.Name, routingKey);
        }

        public BasicGetResult Listen(int waitForMilliseconds = 0, bool suppressDisposal = false, bool ack = true)
        {
            try
            {
                if (waitForMilliseconds > 0)
                    Task.Delay(waitForMilliseconds).Wait();                

                return _channel.BasicGet(_channelName, false);
            }
            finally
            {
                if (!suppressDisposal)
                {
                    //Added wait as rabbit needs some time to sort it self out and the close and dispose was happening to quickly
                    Task.Delay(200).Wait();
                    _channel.Dispose();
                    if (_connection.IsOpen) _connection.Dispose();
                }
            }
        }
    }

    internal static class TestRMQExtensions
    {
        public static string GetBody(this BasicGetResult result)
        {
            return result == null ? null : Encoding.UTF8.GetString(result.Body);
        }

        public static IDictionary<string, object> GetHeaders(this BasicGetResult result)
        {
            return result == null ? null : result.BasicProperties.Headers;
        }
    }
}