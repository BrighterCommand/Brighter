// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 08-26-2015
//
// Last Modified By : ian
// Last Modified On : 08-26-2015
// ***********************************************************************
// <copyright file="ControlBusSenderFactory.cs" company="Ian Cooper">
//     Copyright \u00A9  2014 Ian Cooper
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class ControlBusSenderFactory. Helper for creating instances of a control bus (which requires messaging, but not subcribers).
    /// </summary>
    public class ControlBusSenderFactory : IAmAControlBusSenderFactory {
        /// <summary>
        /// Creates the specified configuration.
        /// </summary>
        /// <param name="gateway">The gateway to the control bus to send messages</param>
        /// <param name="logger">The logger to use</param>
        /// <param name="messageStore">The message store for outgoing messages to the control bus</param>
        /// <returns>IAmAControlBusSender.</returns>
        public IAmAControlBusSender Create(IAmAMessageStore<Message> messageStore, IAmAMessageProducer gateway)
        {
            var mapper = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MonitorEventMessageMapper()));
            mapper.Register<MonitorEvent, MonitorEventMessageMapper>();

            return new ControlBusSender(CommandProcessorBuilder.With()
                    .Handlers(new HandlerConfiguration())
                    .DefaultPolicy()
                    .TaskQueues(new MessagingConfiguration(messageStore, gateway, mapper))
                    .RequestContextFactory(new InMemoryRequestContextFactory())
                    .Build()
                );
        }
    }
}