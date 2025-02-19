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
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    public static class ExchangeConfigurationHelper
    {
        public static void DeclareExchangeForConnection(this IModel channel, RmqMessagingGatewayConnection connection, OnMissingChannel onMissingChannel)
        {
            if (connection.Exchange == null)
                throw new ConfigurationException("RmqMessagingGatewayConnection.Exchange is not set");
            
            if (onMissingChannel == OnMissingChannel.Assume)
                return;

            if (onMissingChannel == OnMissingChannel.Create)
            {
                CreateExchange(channel, connection);
            }
            else if (onMissingChannel == OnMissingChannel.Validate)
            {
                ValidateExchange(channel, connection);
            }
        }

        private static void CreateExchange(IModel channel, RmqMessagingGatewayConnection connection)
        {
            var arguments = new Dictionary<string, object>();
            if (connection.Exchange!.SupportDelay)
            {
                arguments.Add("x-delayed-type", connection.Exchange.Type);
                connection.Exchange.Type = "x-delayed-message";
            }

            channel.ExchangeDeclare(
                connection.Exchange.Name, 
                connection.Exchange.Type, 
                connection.Exchange.Durable, 
                autoDelete: false,
                arguments: arguments);

            if (connection.DeadLetterExchange == null)
                return;
            
            channel.ExchangeDeclare(
                connection.DeadLetterExchange.Name, 
                connection.DeadLetterExchange.Type, 
                connection.DeadLetterExchange.Durable, 
                autoDelete: false);
        }
        private static void ValidateExchange(IModel channel, RmqMessagingGatewayConnection connection)
        
        {
            try
            {
                channel.ExchangeDeclarePassive(connection.Exchange!.Name);
                if (connection.DeadLetterExchange == null)
                    return;
                
                channel.ExchangeDeclarePassive(connection.DeadLetterExchange.Name);
            }
            catch (Exception e)
            {
                throw new BrokerUnreachableException(e);
            }
        }

     }
}
