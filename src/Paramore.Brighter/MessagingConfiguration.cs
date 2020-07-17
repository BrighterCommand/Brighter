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

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessagingConfiguration.
    /// Used to set the components of a work queue solution
    /// </summary>
    public class MessagingConfiguration
    {
        /// When do we timeout talking to the message oriented middleware
        /// </summary>
        public int MessagingGatewaySendTimeout { get; }
        /// <summary>
        /// Gets the message producer.
        /// </summary>
        /// <value>The message producer.</value>
        public IAmAMessageProducer MessageProducer { get; }
        /// <summary>
        /// Gets the message producer that supports async/await.
        /// </summary>
        /// <value>The message producer.</value>
        public IAmAMessageProducerAsync MessageProducerAsync { get; }
        /// <summary>
        /// Gets the message mapper registry.
        /// </summary>
        /// <value>The message mapper registry.</value>
        public IAmAMessageMapperRegistry MessageMapperRegistry { get; }
        /// <summary>
        /// When do we timeout writing to the outbox
        /// </summary>
        public int OutboxWriteTimeout { get; }
        /// <summary>
        /// Gets the outbox.
        /// </summary>
        /// <value>The outbox.</value>
        public IAmAnOutbox<Message> OutBox { get; }
        /// <summary>
        /// Gets the outbox that supports async/await.
        /// </summary>
        /// <value>The outbox.</value>
        public IAmAnOutboxAsync<Message> OutboxAsync { get; }
         /// <summary>
        /// Sets a channel factory. We need this for RPC which has to create a channel itself, but otherwise
        /// this tends to he handled by a Dispatcher not a Command Processor. 
        /// </summary>
        public IAmAChannelFactory ResponseChannelFactory { get; }

        /// <summary>
        /// The configuration of our inbox
        /// </summary>
         public InboxConfiguration UseInbox { get;}
        

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingConfiguration"/> class.
        /// </summary>
        /// <param name="outBox">The outBox.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="outboxWriteTimeout">How long to wait when writing to the outbox</param>
        /// <param name="messagingGatewaySendTimeout">How long to wait when sending via the gateway</param>
        /// <param name="responseChannelFactory">in a request-response scenario how do we build response pipelie</param>
        /// <param name="useInbox">Do we want to create an inbox globally i.e. on every handler (as opposed to by hand). Defaults to null, ,by hand</param>
        public MessagingConfiguration(
            IAmAnOutbox<Message> outBox,
            IAmAMessageProducer messageProducer,
            IAmAMessageMapperRegistry messageMapperRegistry,
            int outboxWriteTimeout = 300,
            int messagingGatewaySendTimeout = 300,
            IAmAChannelFactory responseChannelFactory = null,
            InboxConfiguration useInbox = null
            )
        {
            OutBox = outBox;
            MessageProducer = messageProducer;
            MessageMapperRegistry = messageMapperRegistry;
            OutboxWriteTimeout = outboxWriteTimeout;
            MessagingGatewaySendTimeout = messagingGatewaySendTimeout;
            ResponseChannelFactory = responseChannelFactory;
            UseInbox = useInbox;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingConfiguration"/> class.
        /// </summary>
        /// <param name="outboxAsync">The OutBox which supports async/await.</param>
        /// <param name="asyncMessageProducer">The messaging gateway that supports async/await.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="outboxWriteTimeout">How long to wait when writing to the outbox</param>
        /// <param name="messagingGatewaySendTimeout">How long to wait when sending via the gateway</param>
        /// <param name="useInbox">Do we want to create an inbox globally i.e. on every handler (as opposed to by hand). Defaults to null, by hand</param>
        public MessagingConfiguration(
            IAmAnOutboxAsync<Message> outboxAsync,
            IAmAMessageProducerAsync asyncMessageProducer,
            IAmAMessageMapperRegistry messageMapperRegistry,
            int outboxWriteTimeout = 300,
            int messagingGatewaySendTimeout = 300,
            InboxConfiguration useInbox = null
             )
        {
            OutboxAsync = outboxAsync;
            MessageProducerAsync = asyncMessageProducer;
            MessageMapperRegistry = messageMapperRegistry;
            OutboxWriteTimeout = outboxWriteTimeout;
            MessagingGatewaySendTimeout = messagingGatewaySendTimeout;
            UseInbox = useInbox;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingConfiguration"/> class.
        /// </summary>
        /// <param name="outBox">The OutBox.</param>
        /// <param name="outboxAsync">The OutBox that supports async/await.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="asyncMessageProducer">The messaging gateway that supports async/await.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="outboxWriteTimeout">How long to wait when writing to the outbox</param>
        /// <param name="messagingGatewaySendTimeout">How long to wait when sending via the gateway</param>
        /// <param name="useInbox">Do we want to create an inbox globally i.e. on every handler (as opposed to by hand). Defaults to null, by hand</param>
        public MessagingConfiguration(
            IAmAnOutbox<Message> outBox,
            IAmAnOutboxAsync<Message> outboxAsync,
            IAmAMessageProducer messageProducer,
            IAmAMessageProducerAsync asyncMessageProducer,
            IAmAMessageMapperRegistry messageMapperRegistry,
            int outboxWriteTimeout = 300,
            int messagingGatewaySendTimeout = 300,
            InboxConfiguration useInbox = null
          )
        {
            OutBox = outBox;
            OutboxAsync = outboxAsync;
            MessageProducer = messageProducer;
            MessageProducerAsync = asyncMessageProducer;
            MessageMapperRegistry = messageMapperRegistry;
            OutboxWriteTimeout = outboxWriteTimeout;
            MessagingGatewaySendTimeout = messagingGatewaySendTimeout;
            UseInbox = useInbox;
        }
    }
}
