using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        private readonly IAmADynamoDbTransactionProvider _transactionProvider;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandler> _logger;

        public GreetingMadeHandler(IAmADynamoDbTransactionProvider transactionProvider, IAmACommandProcessor postBox, ILogger<GreetingMadeHandler> logger)
        {
            _transactionProvider = transactionProvider;
            _postBox = postBox;
            _logger = logger;
        }

        [UseInbox(step:0, contextKey: typeof(GreetingMadeHandler), onceOnly: true )] 
        [RequestLogging(step: 1, timing: HandlerTiming.Before)]
        [UsePolicy(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY)]
        public override GreetingMade Handle(GreetingMade @event)
        {
            var posts = new List<Guid>();
            var context = new DynamoDBContext(_transactionProvider.DynamoDb);
            var tx = _transactionProvider.GetTransaction();
            try
            {
                var salutation = new Salutation { Greeting = @event.Greeting };
                var attributes = context.ToDocument(salutation).ToAttributeMap();

                tx.TransactItems.Add(new TransactWriteItem
                {
                    Put = new Put { TableName = "Salutations", Item = attributes }
                });

                posts.Add(_postBox.DepositPost(new SalutationReceived(DateTimeOffset.Now), _transactionProvider));

                _transactionProvider.Commit();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                _transactionProvider.Rollback();

                return base.Handle(@event);
            }
            finally
            {
                _transactionProvider.Close();
            }

            _postBox.ClearOutboxAsync(posts);
            
            return base.Handle(@event);
        }
    }
}
