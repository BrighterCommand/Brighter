using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        private readonly IUnitOfWork _uow;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandler> _logger;

        /*
         * KAFKA and ASYNC: Kafka is oriented around the idea of an ordered append log of events. You will lose that ordering
         * if you use an async handler, because your handlers will not necessarily complete in order. Because a Brighter consumer
         * is single-threaded we guarantee your ordering, provided you don't use async handlers. If you do, there are no
         * guarantees about order.
         * Generally, you should not use async handlers with Kafka, unless you are happy to lose ordering.
         * Instead, rely on being able to partition your topic such that a single thread can handle the number of messages
         * arriving on that thread with an acceptable latency.
         */
        public GreetingMadeHandler(IUnitOfWork uow, IAmACommandProcessor postBox, ILogger<GreetingMadeHandler> logger)
        {
            _uow = uow;
            _postBox = postBox;
            _logger = logger;
        }

        //[UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] -- we are using a global inbox, so need to be explicit!!
        [RequestLogging(step: 1, timing: HandlerTiming.Before)]
        [UsePolicy(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY)]
        public override GreetingMade Handle(GreetingMade @event)
        {
            var posts = new List<Guid>();
            
            var tx = _uow.BeginOrGetTransaction();
            try
            {
                var salutation = new Salutation(@event.Greeting);
                
                _uow.Database.Insert<Salutation>(salutation, tx);
                
                posts.Add(_postBox.DepositPost(new SalutationReceived(DateTimeOffset.Now)));
                
                tx.Commit();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                
                //if it went wrong rollback entity write and Outbox write
                tx.Rollback();
                
                return base.Handle(@event);
            }

            _postBox.ClearOutbox(posts.ToArray());
            
            return base.Handle(@event);
        }
    }
}
