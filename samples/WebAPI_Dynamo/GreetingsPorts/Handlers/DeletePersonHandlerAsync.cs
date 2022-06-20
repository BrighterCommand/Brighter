using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly DynamoDbUnitOfWork _unitOfWork;

        public DeletePersonHandlerAsync(DynamoDbUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async override Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            var context = new DynamoDBContext(_unitOfWork.DynamoDb);
            await context.DeleteAsync(deletePerson.Name, cancellationToken);

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
