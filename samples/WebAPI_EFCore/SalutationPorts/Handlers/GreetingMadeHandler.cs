using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.EntityGateway;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        private readonly SalutationsEntityGateway _uow;
        private readonly IAmACommandProcessor _postBox;
        private readonly IAmATransactionConnectionProvider _transactionProvider;

        public GreetingMadeHandler(SalutationsEntityGateway uow, IAmATransactionConnectionProvider provider, IAmACommandProcessor postBox)
        {
            _uow = uow;
            _postBox = postBox;
            _transactionProvider = provider;
        }

        [UseInbox(step:0, contextKey: typeof(GreetingMadeHandler), onceOnly: true )] 
        [RequestLogging(step: 1, timing: HandlerTiming.Before)]
        [UsePolicy(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY)]
        public override GreetingMade Handle(GreetingMade @event)
        {
            var posts = new List<Guid>();
            
            var tx =_transactionProvider.GetTransaction();
            try
            {
                var salutation = new Salutation(@event.Greeting);

                _uow.Salutations.Add(salutation);

                posts.Add(_postBox.DepositPost(
                    new SalutationReceived(DateTimeOffset.Now),
                    _transactionProvider)
                );

                _uow.SaveChanges();

                _transactionProvider.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                _transactionProvider.Rollback();

                Console.WriteLine("Salutation analytical record not saved");

                throw;
            }
            finally
            {
                _transactionProvider.Close();
            }

            _postBox.ClearOutbox(posts.ToArray());
            
            return base.Handle(@event);
        }
    }
}
