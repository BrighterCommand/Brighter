using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly DynamoDbUnitOfWork _unitOfWork;

        public DeletePersonHandlerAsync(IAmABoxTransactionConnectionProvider unitOfWork)
        {
            _unitOfWork = (DynamoDbUnitOfWork)unitOfWork;
        }
        
        [RequestLogging(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public async override Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            var context = new DynamoDBContext(_unitOfWork.DynamoDb);
            await context.DeleteAsync(deletePerson.Name, cancellationToken);

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
