using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly DynamoDbUnitOfWork _dynamoDbUnitOfWork;

        public AddPersonHandlerAsync(DynamoDbUnitOfWork dynamoDbUnitOfWork )
        {
            _dynamoDbUnitOfWork = dynamoDbUnitOfWork;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            var context = new DynamoDBContext(_dynamoDbUnitOfWork.DynamoDb);
            await context.SaveAsync(new Person { Name = addPerson.Name }, cancellationToken);

            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
