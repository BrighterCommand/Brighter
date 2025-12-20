#region Licence
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
    public partial class ConfigurationCommandHandler : RequestHandler<ConfigurationCommand>
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
        /// <param name="configurationCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override ConfigurationCommand Handle(ConfigurationCommand configurationCommand)
        {
            Log.HandlingConfigurationCommand(s_logger, configurationCommand.Type);

            switch (configurationCommand.Type)
            {
                case ConfigurationCommandType.CM_STOPALL:
                    Log.StoppingAllConsumersBegin(s_logger, DateTime.UtcNow.ToString("o"));
                    Log.LogSeparator(s_logger);
                    Log.LogEllipsis(s_logger);
                    _dispatcher.End().Wait();
                    Log.AllConsumersStopped(s_logger, DateTime.UtcNow.ToString("o"));
                    Log.LogSeparator(s_logger);
                    break;
                case ConfigurationCommandType.CM_STARTALL:
                    Log.LogSeparator(s_logger);
                    Log.StartingAllConsumersBegin(s_logger, DateTime.UtcNow.ToString("o"));
                    Log.LogSeparator(s_logger);
                    _dispatcher.Receive();
                    break;
                case ConfigurationCommandType.CM_STOPCHANNEL:
                    Log.LogSeparator(s_logger);
                    Log.StoppingChannel(s_logger, configurationCommand.SubscriptionName);
                    Log.LogSeparator(s_logger);
                    _dispatcher.Shut(new SubscriptionName(configurationCommand.SubscriptionName));
                    break;
                case ConfigurationCommandType.CM_STARTCHANNEL:
                    Log.LogSeparator(s_logger);
                    Log.StartingChannel(s_logger, configurationCommand.SubscriptionName);
                    Log.LogSeparator(s_logger);
                    _dispatcher.Open(new SubscriptionName(configurationCommand.SubscriptionName));
                    break;
                default:
                    throw new ArgumentException("{0} is an unknown Configuration Command", Enum.GetName(typeof(ConfigurationCommandType), configurationCommand.Type));
            }

            return base.Handle(configurationCommand);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "Handling Configuration Command of Type: {CommandType}")]
            public static partial void HandlingConfigurationCommand(ILogger logger, ConfigurationCommandType commandType);

            [LoggerMessage(LogLevel.Debug, "Configuration Command received and now stopping all consumers. Begin at {Time}")]
            public static partial void StoppingAllConsumersBegin(ILogger logger, string time);

            [LoggerMessage(LogLevel.Debug, "All consumers stopped in response to configuration command. Stopped at {Time}")]
            public static partial void AllConsumersStopped(ILogger logger, string time);

            [LoggerMessage(LogLevel.Debug, "Configuration Command received and now starting all consumers. Begin at {Time}")]
            public static partial void StartingAllConsumersBegin(ILogger logger, string time);

            [LoggerMessage(LogLevel.Debug, "Configuration Command received and now stopping channel {ChannelName}")]
            public static partial void StoppingChannel(ILogger logger, string channelName);

            [LoggerMessage(LogLevel.Debug, "Configuration Command received and now starting channel {ChannelName}")]
            public static partial void StartingChannel(ILogger logger, string channelName);
            
            [LoggerMessage(LogLevel.Debug, "--------------------------------------------------------------------------")]
            public static partial void LogSeparator(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "...")]
            public static partial void LogEllipsis(ILogger logger);
        }
    }
}

