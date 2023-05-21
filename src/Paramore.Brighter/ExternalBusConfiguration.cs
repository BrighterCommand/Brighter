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

namespace Paramore.Brighter
{
    /// <summary>
    /// Used to configure the Event Bus
    /// </summary>
    public class ExternalBusConfiguration
    {
        /// <summary>
        /// The registry is a collection of producers 
        /// </summary>
        /// <value>The registry of producers</value>
        public IAmAProducerRegistry ProducerRegistry { get; set; }

        /// <summary>
        /// Gets the message mapper registry.
        /// </summary>
        /// <value>The message mapper registry.</value>
        public IAmAMessageMapperRegistry MessageMapperRegistry { get; set; }

        /// <summary>
        /// The Outbox we wish to use for messaging
        /// </summary>
        public IAmAnOutbox Outbox { get; set; }

        /// <summary>
        /// The maximum amount of messages to deposit into the outbox in one transmissions.
        /// This is to stop insert statements getting too big
        /// </summary>
        public int OutboxBulkChunkSize { get; set; }

        /// <summary>
        /// When do we timeout writing to the outbox
        /// </summary>
        public int OutboxWriteTimeout { get; set; }
        
        /// <summary>
        /// Sets a channel factory. We need this for RPC which has to create a channel itself, but otherwise
        /// this tends to he handled by a Dispatcher not a Command Processor. 
        /// </summary>
        public IAmAChannelFactory ResponseChannelFactory { get; set; }
        
        /// <summary>
        /// Sets up a transform factory. We need this if you have transforms applied to your MapToMessage or MapToRequest methods
        /// of your MessageMappers
        /// </summary>
        public IAmAMessageTransformerFactory TransformerFactory { get; set; }

        /// <summary>
        /// The configuration of our inbox
        /// </summary>
        public InboxConfiguration UseInbox { get; set; }

        /// <summary>
        /// Should we use an in-memory outbox
        /// </summary>
        public bool UseInMemoryOutbox { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalBusConfiguration"/> class.
        /// </summary>
        public ExternalBusConfiguration()
        {
           /*allows setting of properties one-by-one*/ 
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalBusConfiguration"/> class.
        /// </summary>
        /// <param name="producerRegistry">Clients for the external bus by topic they send to. The client details are specialised by transport</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="outbox">The outbox we wish to use for messaging</param>
        /// <param name="outboxBulkChunkSize">The maximum amount of messages to deposit into the outbox in one transmissions.</param>
        /// <param name="outboxWriteTimeout">How long to wait when writing to the outbox</param>
        /// <param name="responseChannelFactory">in a request-response scenario how do we build response pipeline</param>
        /// <param name="transformerFactory">The factory that builds instances of a transforms for us</param>
        /// <param name="useInbox">Do we want to create an inbox globally i.e. on every handler (as opposed to by hand). Defaults to null, ,by hand</param>
        public ExternalBusConfiguration(
            IAmAProducerRegistry producerRegistry,
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAnOutbox outbox,
            int outboxBulkChunkSize = 100,
            int outboxWriteTimeout = 300,
            IAmAChannelFactory responseChannelFactory = null,
            IAmAMessageTransformerFactory transformerFactory = null,
            InboxConfiguration useInbox = null)
        {
            ProducerRegistry = producerRegistry;
            MessageMapperRegistry = messageMapperRegistry;
            Outbox = outbox;
            OutboxWriteTimeout = outboxWriteTimeout;
            ResponseChannelFactory = responseChannelFactory;
            UseInbox = useInbox;
            OutboxBulkChunkSize = outboxBulkChunkSize;
            TransformerFactory = transformerFactory;
        }
    }
}
