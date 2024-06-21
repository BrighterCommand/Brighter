using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using GreetingsEntities;
using GreetingsPorts.Policies;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Brighter.DynamoDb;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        private readonly IAmADynamoDbConnectionProvider _dynamoDbConnectionProvider;

        public FindPersonByNameHandlerAsync(IAmADynamoDbConnectionProvider dynamoDbConnectionProvider)
        {
            _dynamoDbConnectionProvider = dynamoDbConnectionProvider;
        }

        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            var context = new DynamoDBContext(_dynamoDbConnectionProvider.DynamoDb);

            var person = await context.LoadAsync<Person>(query.Name, cancellationToken);

            if (person == null)
            {
                throw new InvalidOperationException($"There is no person called {query.Name} registered");
            }

            return new FindPersonResult(person);
        }
    }
}
