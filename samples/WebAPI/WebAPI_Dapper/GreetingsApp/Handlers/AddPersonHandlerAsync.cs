using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers;

public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
{
    private readonly IAmARelationalDbConnectionProvider _relationalDbConnectionProvider;

    public AddPersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
    {
        _relationalDbConnectionProvider = relationalDbConnectionProvider;
    }

    [RequestLoggingAsync(0, HandlerTiming.Before)]
    [UsePolicyAsync(step: 1, policy: Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
    public override async Task<AddPerson> HandleAsync(AddPerson addPerson,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection =
            await _relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("insert into Person (Name) values (@Name)", new { addPerson.Name });
        return await base.HandleAsync(addPerson, cancellationToken);
    }
}
