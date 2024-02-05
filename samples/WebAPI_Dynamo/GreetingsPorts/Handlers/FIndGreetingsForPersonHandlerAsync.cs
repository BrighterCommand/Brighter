using System.Linq;
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
    public class FIndGreetingsForPersonHandlerAsync(IAmADynamoDbConnectionProvider dynamoDbConnectionProvider)
        : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings>
    {
        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonsGreetings> ExecuteAsync(FindGreetingsForPerson query, CancellationToken cancellationToken = new CancellationToken())
        {
            var context = new DynamoDBContext(dynamoDbConnectionProvider.DynamoDb);

            var person = await context.LoadAsync<Person>(query.Name, cancellationToken);

            return new FindPersonsGreetings { Greetings = person.Greetings.Select(g => new Salutation(g)).ToList(), Name = query.Name };

        }
        
    }
}
