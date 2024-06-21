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
        private readonly IAmADynamoDbConnectionProvider _dynamoDbConnectionProvider;

        public DeletePersonHandlerAsync(IAmADynamoDbConnectionProvider dynamoDbConnectionProvider)
        {
            _dynamoDbConnectionProvider = dynamoDbConnectionProvider;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            var context = new DynamoDBContext(_dynamoDbConnectionProvider.DynamoDb);
            await context.DeleteAsync(deletePerson.Name, cancellationToken);

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
