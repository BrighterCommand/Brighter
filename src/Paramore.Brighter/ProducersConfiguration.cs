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
using System.Linq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public interface IAmProducersConfiguration
    {
        /// <summary>
        /// How big should the batch size be for archiving messages
        /// </summary>
        public int? ArchiveBatchSize { get; set; }

        /// <summary>
        /// If we want to archive messages, abstracts archival storage
        /// </summary>
        public IAmAnArchiveProvider? ArchiveProvider { get; set; }
        
        /// <summary>
        /// How do obtain a connection to the Outbox that is not part of a shared transaction.
        /// NOTE: Must implement IAmARelationalDbConnectionProvider
        /// </summary>
        Type? ConnectionProvider { get; set; }
 
        /// <summary>
        /// The Distributed Locking Service
        /// </summary>
        public IDistributedLock? DistributedLock { get; set; }
        
        /// <summary>
        /// Gets the message mapper registry.
        /// You can set this, but you will not need to if you are using the AutoFromAssemblies extension method
        /// </summary>
        /// <value>The message mapper registry.</value>
        IAmAMessageMapperRegistry? MessageMapperRegistry { get; set; }

        /// <summary>
        /// The Outbox we wish to use for messaging
        /// </summary>
        IAmAnOutbox? Outbox { get; set; }

        /// <summary>
        /// The maximum amount of messages to deposit into the outbox in one transmissions.
        /// This is to stop insert statements getting too big
        /// </summary>
        int? OutboxBulkChunkSize { get; set; }

        /// <summary>
        /// When do we timeout writing to the outbox
        /// </summary>
        int? OutboxTimeout { get; set; }
        
        /// <summary>
        /// How many outstanding messages may the outbox have before we terminate the programme with an OutboxLimitReached exception?
        /// -1 => No limit, although the Outbox may discard older entries which is implementation dependent
        /// 0 => No outstanding messages, i.e. throw an error as soon as something goes into the Outbox
        /// 1+ => Allow this number of messages to stack up in an Outbox before throwing an exception (likely to fail fast)
        /// </summary>
        public int MaxOutStandingMessages { get; set; } 

        /// <summary>
        /// At what interval should we check the number of outstanding messages has not exceeded the limit set in MaxOutStandingMessages
        /// We spin off a thread to check when inserting an item into the outbox, if the interval since the last insertion is greater than this threshold
        /// If you set MaxOutStandingMessages to 0 this property is effectively ignored
        /// </summary>
        public TimeSpan MaxOutStandingCheckInterval { get; set; } 
        
        /// <summary>
        /// An outbox may require additional arguments before it can run its checks. The DynamoDb outbox for example expects there to be a Topic in the args
        /// This bag provides the args required
        /// </summary>
        Dictionary<string, object>? OutBoxBag { get; set; }
        
        /// <summary>
        /// The registry is a collection of producers 
        /// </summary>
        /// <value>The registry of producers</value>
        IAmAProducerRegistry? ProducerRegistry { get; set; }
        
        /// <summary>
        /// Sets a channel factory. We need this for RPC which has to create a channel itself, but otherwise
        /// this tends to he handled by a Dispatcher not a Command Processor. 
        /// </summary>
        IAmAChannelFactory? ResponseChannelFactory { get; set; }

        /// <summary>
        /// If we are using Rpc, what are the subscriptions for the reply queue?
        /// </summary>
        IEnumerable<Subscription>? ReplyQueueSubscriptions { get; set; }
        
        /// <summary>
        /// Do you want to override the default in memory request context factory? Used for callbacks
        /// </summary>
        IAmARequestContextFactory? RequestContextFactory { get; set; }
        
        /// <summary>
        /// The Message Scheduler Factory.
        /// </summary>
        IAmAMessageSchedulerFactory? MessageSchedulerFactory { get; set; }
        
        /// <summary>
        /// The Request Scheduler Factory.
        /// </summary>
        /// <remarks>
        /// The default will be in-memory
        /// </remarks>
        IAmARequestSchedulerFactory? RequestSchedulerFactory { get; set; }
        
        /// <summary>
        /// The transaction provider for the outbox
        /// </summary>
        Type? TransactionProvider { get; set; }

        /// <summary>
        /// Do we want to support RPC on an external bus?
        /// </summary>
        bool UseRpc { get; set; }

    }

    /// <summary>
    /// Used to configure the Event Bus
    /// </summary>
    public class ProducersConfiguration : IAmProducersConfiguration
    {
        /// <summary>
        /// How big should the batch size be for archiving messages
        /// </summary>
        public int? ArchiveBatchSize { get; set; }

        /// <summary>
        /// If we want to archive messages, abstracts archival storage
        /// </summary>
        public IAmAnArchiveProvider? ArchiveProvider { get; set; }
        
        /// <summary>
        /// How do obtain a connection to the Outbox that is not part of a shared transaction.
        /// NOTE: Must implement IAmARelationalDbConnectionProvider
        /// </summary>
        public Type? ConnectionProvider { get; set; }
        
        /// <summary>
        /// The Distributed Locking Service
        /// </summary>
        public IDistributedLock? DistributedLock { get; set; }
        
        /// <summary>
        /// How verbose should our instrumentation be?
        /// InstrumentationOptions.None - no instrumentation
        /// InstrumentationOptions.RequestInformation - just the request id, request type and operation
        /// InstrumentationOptions.RequestBody - the request body
        /// InstrumentationOptions.RequestContext - the request context
        /// InstrumentationOptions.All - all of the above
        /// </summary>
        public InstrumentationOptions? InstrumentationOptions { get; set; }
        
        /// <summary>
        /// Gets the message mapper registry.
        /// You can set this, but you will not need to if you are using the AutoFromAssemblies extension method
        /// </summary>
        /// <value>The message mapper registry.</value>
        public IAmAMessageMapperRegistry? MessageMapperRegistry { get; set; }

        /// <summary>
        /// How many outstanding messages may the outbox have before we terminate the programme with an OutboxLimitReached exception?
        /// -1 => No limit, although the Outbox may discard older entries which is implementation dependent
        /// 0 => No outstanding messages, i.e. throw an error as soon as something goes into the Outbox
        /// 1+ => Allow this number of messages to stack up in an Outbox before throwing an exception (likely to fail fast)
        /// </summary>
        public int MaxOutStandingMessages { get; set; } = -1;

        /// <summary>
        /// At what interval should we check the number of outstanding messages has not exceeded the limit set in MaxOutStandingMessages
        /// We spin off a thread to check when inserting an item into the outbox, if the interval since the last insertion is greater than this threshold
        /// If you set MaxOutStandingMessages to -1 or 0 this property is effectively ignored
        /// </summary>
        public TimeSpan MaxOutStandingCheckInterval { get; set; } = TimeSpan.Zero;
        
        /// <summary>
        /// The Outbox we wish to use for messaging
        /// </summary>
        public IAmAnOutbox? Outbox { get; set; }
   
        /// <summary>
        /// The maximum amount of messages to deposit into the outbox in one transmissions.
        /// This is to stop insert statements getting too big
        /// </summary>
        public int? OutboxBulkChunkSize { get; set; }
        
        /// <summary>
        /// An outbox may require additional arguments before it can run its checks. The DynamoDb outbox for example expects there to be a Topic in the args
        /// This bag provides the args required
        /// </summary>
        public Dictionary<string, object>? OutBoxBag { get; set; }

        /// <summary>
        /// When do we timeout writing to the outbox
        /// </summary>
        public int? OutboxTimeout { get; set; }
        
        /// <summary>
        /// The registry is a collection of producers 
        /// </summary>
        /// <value>The registry of producers</value>
        public IAmAProducerRegistry? ProducerRegistry { get; set; }
        
        /// <summary>
        /// If we are using Rpc, what are the subscriptions for the reply queue?
        /// </summary>
        public IEnumerable<Subscription>? ReplyQueueSubscriptions { get; set; }
        
        /// <summary>
        /// Sets a channel factory. We need this for RPC which has to create a channel itself, but otherwise
        /// this tends to he handled by a Dispatcher not a Command Processor. 
        /// </summary>
        public IAmAChannelFactory? ResponseChannelFactory { get; set; }
        
        /// <summary>
        /// Do you want to override the default in memory request context factory? Used for callbacks
        /// </summary>
        public IAmARequestContextFactory? RequestContextFactory { get; set; }
        
        /// <summary>
        /// Sets up a transform factory. We need this if you have transforms applied to your MapToMessage or MapToRequest methods
        /// of your MessageMappers
        /// You can set this, but you will not need to if you are using the AutoFromAssemblies extension method
        /// </summary>
        public IAmAMessageTransformerFactory? TransformerFactory { get; set; }
        
        /// <summary>
        /// The Message Scheduler Factory.
        /// </summary>
        /// <remarks>
        /// The default will be in-memory
        /// </remarks>
        public IAmAMessageSchedulerFactory? MessageSchedulerFactory { get; set; }

        /// <summary>
        /// The Request Scheduler Factory.
        /// </summary>
        /// <remarks>
        /// The default will be in-memory
        /// </remarks>
        public IAmARequestSchedulerFactory? RequestSchedulerFactory { get; set; }
        
        /// <summary>
        /// The transaction provider for the outbox
        /// NOTE: Must implement IAmABoxTransactionProvider&lt; &gt;
        /// </summary>
        public Type? TransactionProvider { get; set; }

        /// <summary>
        /// Do we want to support RPC on an external bus?
        /// </summary>
        public bool UseRpc { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProducersConfiguration"/> class.
        /// </summary>
        public ProducersConfiguration()
        {
           /*allows setting of properties one-by-one, we default the required values here*/
           ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
        }

    }
}
