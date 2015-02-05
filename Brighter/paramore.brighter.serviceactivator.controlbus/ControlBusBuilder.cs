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

using System.Collections.Generic;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguration;

namespace paramore.brighter.serviceactivator.controlbus
{
    public class ControlBusBuilder
    {
        ILog logger;
        CommandProcessor commandProcessor;
        IAmAChannelFactory channelFactory;

        public ControlBusBuilder ChannelFactory(IAmAChannelFactory channelFactory)
        {
            this.channelFactory = channelFactory;
            return this;
        }

        public ControlBusBuilder CommandProcessor(CommandProcessor commandProcessor)
        {
            this.commandProcessor = commandProcessor;
            return this;
        }

        public ControlBusBuilder Logger(ILog logger)
        {
            this.logger = logger;
            return this;
        }

        public Dispatcher Build(string hostName)
        {
            var connections = new List<ConnectionElement>();
            //These are the control bus channels, we hardcode them because we want to know they exist, but we use
            //a base naming scheme to allow centralized management.
            const string CONFIGURATION = "configuration";
            var configurationElement = new ConnectionElement
            {
                ChannelName = CONFIGURATION, 
                ConnectionName = CONFIGURATION, 
                RoutingKey = hostName + "." + CONFIGURATION,

            };
            connections.Add(configurationElement);

            const string HEARTBEAT = "heartbeat";
            var heartbeatElement = new ConnectionElement
            {
                ChannelName = CONFIGURATION, 
                ConnectionName = CONFIGURATION, 
                RoutingKey = hostName + "." + CONFIGURATION,

            };
            connections.Add(heartbeatElement );

            
            return DispatchBuilder
                .With()
                .Logger(logger)
                .CommandProcessor(commandProcessor)
                .MessageMappers(new MessageMapperRegistry(new ControlBusMessageMapperFactory()))
                .ChannelFactory(channelFactory)
                .ConnectionsFromElements(connections)
                .Build();
        }
    }
}
