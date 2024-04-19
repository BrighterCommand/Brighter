using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly IAmADynamoDbConnectionProvider _dynamoDbConnectionProvider;

        public AddPersonHandlerAsync(IAmADynamoDbConnectionProvider dynamoDbConnectionProvider)
        {
            _dynamoDbConnectionProvider = dynamoDbConnectionProvider;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default)
        {
            var context = new DynamoDBContext(_dynamoDbConnectionProvider.DynamoDb);
            await context.SaveAsync(new Person { Name = addPerson.Name }, cancellationToken);

            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
