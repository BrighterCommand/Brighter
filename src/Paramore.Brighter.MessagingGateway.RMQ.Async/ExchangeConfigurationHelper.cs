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
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

public static class ExchangeConfigurationHelper
{
    public static async Task DeclareExchangeForConnection(this IChannel channel, RmqMessagingGatewayConnection connection,
        OnMissingChannel onMissingChannel, 
        CancellationToken cancellationToken = default)
    {
        if (onMissingChannel == OnMissingChannel.Assume)
        {
            return;
        }

        if (onMissingChannel == OnMissingChannel.Create)
        {
            await CreateExchange(channel, connection, cancellationToken);
        }
        else if (onMissingChannel == OnMissingChannel.Validate)
        {
            await ValidateExchange(channel, connection, cancellationToken);
        }
    }

    private static async Task CreateExchange(IChannel channel, RmqMessagingGatewayConnection connection, CancellationToken cancellationToken)
    {
        if (connection.Exchange is null) throw new ConfigurationException("RabbitMQ Exchange is not set");
        
        var arguments = new Dictionary<string, object?>();
        if (connection.Exchange.SupportDelay)
        {
            arguments.Add("x-delayed-type", connection.Exchange.Type);
            connection.Exchange.Type = "x-delayed-message";
        }

        await channel.ExchangeDeclareAsync(
            connection.Exchange.Name,
            connection.Exchange.Type,
            connection.Exchange.Durable,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);


        if (connection.DeadLetterExchange != null)
        {
            await channel.ExchangeDeclareAsync(
                connection.DeadLetterExchange.Name,
                connection.DeadLetterExchange.Type,
                connection.DeadLetterExchange.Durable,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task ValidateExchange(IChannel channel, RmqMessagingGatewayConnection connection, CancellationToken cancellationToken)
    {
        if (connection.Exchange is null) throw new ConfigurationException("RabbitMQ Exchange is not set");
        
        try
        {
            await channel.ExchangeDeclarePassiveAsync(connection.Exchange.Name, cancellationToken);

            if (connection.DeadLetterExchange != null)
            {
                await channel.ExchangeDeclarePassiveAsync(connection.DeadLetterExchange.Name, cancellationToken);
            }
        }
        catch (Exception e)
        {
            throw new BrokerUnreachableException(e);
        }
    }
}
