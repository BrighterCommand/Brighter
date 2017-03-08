// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 12-01-2015
//
// Last Modified By : ian
// Last Modified On : 12-07-2015
// ***********************************************************************
// <copyright file="HeartbeatRequestCommandHandler.cs" company="Ian Cooper">
//     Copyright \u00A9  2014 Ian Cooper
// </copyright>
// <summary></summary>
// ***********************************************************************
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

using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.ServiceActivator.Ports.Handlers
{
    /// <summary>
    /// Class HeartbeatRequestCommandHandler.
    /// </summary>
    public class HeartbeatRequestCommandHandler : RequestHandler<HeartbeatRequest>
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatRequestCommandHandler"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="dispatcher">The dispatcher.</param>
        public HeartbeatRequestCommandHandler(IAmACommandProcessor commandProcessor, IDispatcher dispatcher)
        {
            _commandProcessor = commandProcessor;
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// We want to send a heartbeat back to the caller. The heartbeat consists of the set of channels we own
        /// and their current status.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override HeartbeatRequest Handle(HeartbeatRequest command)
        {
            var heartbeat = new HeartbeatReply(_dispatcher.HostName, new ReplyAddress(command.ReplyAddress.Topic, command.ReplyAddress.CorrelationId));
            _dispatcher.Consumers.Each((consumer) => heartbeat.Consumers.Add(new RunningConsumer(consumer.Name, consumer.State)));

            _commandProcessor.Post(heartbeat);

            return base.Handle(command);
        }
    }
}
