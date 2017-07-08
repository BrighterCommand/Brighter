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
using Paramore.Brighter.ServiceActivator.Logging;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Handlers
{
    /// <summary>
    /// Class ConfigurationMessageHandler.
    /// </summary>
    public class ConfigurationCommandHandler : RequestHandler<ConfigurationCommand>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<ConfigurationCommandHandler>);

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
            _logger.Value.DebugFormat("Handling Configuration Command of Type: {0}", command.Type);

            switch (command.Type)
            {
                case ConfigurationCommandType.CM_STOPALL:
                    _logger.Value.DebugFormat("Configuration Command received and now stopping all consumers. Begin at {0}", DateTime.UtcNow.ToString("o"));
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _logger.Value.Debug("...");
                    _dispatcher.End().GetAwaiter().GetResult();
                    _logger.Value.DebugFormat("All consumers stopped in response to configuration command. Stopped at {0}", DateTime.UtcNow.ToString("o"));
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    break;
                case ConfigurationCommandType.CM_STARTALL:
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _logger.Value.DebugFormat("Configuration Command received and now starting all consumers. Begin at {0}", DateTime.UtcNow.ToString("o"));
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _dispatcher.Receive();
                    break;
                case ConfigurationCommandType.CM_STOPCHANNEL:
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _logger.Value.DebugFormat("Configuration Command received and now stopping channel {0}", command.ConnectionName);
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _dispatcher.Shut(command.ConnectionName);
                    break;
                case ConfigurationCommandType.CM_STARTCHANNEL:
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _logger.Value.DebugFormat("Configuration Command received and now starting channel {0}", command.ConnectionName);
                    _logger.Value.Debug("--------------------------------------------------------------------------");
                    _dispatcher.Open(command.ConnectionName);
                    break;
                default:
                    throw new ArgumentException("{0} is an unknown Configuration Command", Enum.GetName(typeof(ConfigurationCommandType), command.Type));
            }

            return base.Handle(command);
        }

    }
}
