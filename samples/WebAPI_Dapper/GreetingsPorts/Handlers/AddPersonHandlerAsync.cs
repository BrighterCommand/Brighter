using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
        : RequestHandlerAsync<AddPerson>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default)
        {
            await using var connection = await relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
            await connection.ExecuteAsync("insert into Person (Name) values (@Name)", new {Name = addPerson.Name});
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
