// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="RMQSendMessageGateway.cs" company="">
//     Copyright (c) . All rights reserved.
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

using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class ClientRequestHandler .
    /// The <see cref="RmqMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles connection establishment, request sending and error handling
    /// </summary>
    public class RmqMessageProducer : MessageGateway, IAmAMessageProducer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RmqMessageProducer(ILog logger) : base(logger) { }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public Task Send(Message message)
        {
            //RabbitMQ .NET Client does not have an async publish, so fake this for now as we want to support messaging frameworks that do have this option
            var tcs = new TaskCompletionSource<object>();

            Logger.DebugFormat("RmqMessageProducer: Preparing  to sending message via exchange {0}", Configuration.Exchange.Name);

            try
            {
                EnsureChannel();
                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Configuration.Exchange.Name);
                Logger.DebugFormat("RmqMessageProducer: Publishing message to exchange {0} on connection {1} with topic {2} and id {3} and body: {4}", Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri(), message.Header.Topic, message.Id, message.Body.Value);
                rmqMessagePublisher.PublishMessage(message);
                Logger.InfoFormat("RmqMessageProducer: Published message to exchange {0} on connection {1} with topic {2} and id {3} and message: {4} at {5}", Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri(), message.Header.Topic, message.Id, JsonConvert.SerializeObject(message), DateTime.UtcNow);
            }
            catch (Exception e)
            {
                if (Channel != null)
                    tcs.SetException(e);
                throw;
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="RmqMessageProducer"/> class.
        /// </summary>
        ~RmqMessageProducer()
        {
            Dispose(false);
        }
    }
}