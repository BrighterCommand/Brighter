﻿#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Handlers
{
    /// <summary>
    /// Class ConfigurationMessageHandler.
    /// </summary>
    public class ConfigurationCommandHandler : RequestHandler<ConfigurationCommand>
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<ConfigurationCommandHandler>();

        private readonly IDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCommandHandler" /> class.
        /// </summary>
        /// <param name="dispatcher"></param>
        public ConfigurationCommandHandler(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override ConfigurationCommand Handle(ConfigurationCommand command)
        {
            s_logger.LogDebug("Handling Configuration Command of Type: {CommandType}", command.Type);

            switch (command.Type)
            {
                case ConfigurationCommandType.CM_STOPALL:
                    s_logger.LogDebug("Configuration Command received and now stopping all consumers. Begin at {Time}", DateTime.UtcNow.ToString("o"));
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    s_logger.LogDebug("...");
                    _dispatcher.End().Wait();
                    s_logger.LogDebug("All consumers stopped in response to configuration command. Stopped at {Time}", DateTime.UtcNow.ToString("o"));
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    break;
                case ConfigurationCommandType.CM_STARTALL:
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    s_logger.LogDebug("Configuration Command received and now starting all consumers. Begin at {Time}", DateTime.UtcNow.ToString("o"));
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    _dispatcher.Receive();
                    break;
                case ConfigurationCommandType.CM_STOPCHANNEL:
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    s_logger.LogDebug("Configuration Command received and now stopping channel {ChannelName}", command.SubscriptionName);
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    _dispatcher.Shut(command.SubscriptionName);
                    break;
                case ConfigurationCommandType.CM_STARTCHANNEL:
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    s_logger.LogDebug("Configuration Command received and now starting channel {ChannelName}", command.SubscriptionName);
                    s_logger.LogDebug("--------------------------------------------------------------------------");
                    _dispatcher.Open(command.SubscriptionName);
                    break;
                default:
                    throw new ArgumentException("{0} is an unknown Configuration Command", Enum.GetName(typeof(ConfigurationCommandType), command.Type));
            }

            return base.Handle(command);
        }

    }
}
