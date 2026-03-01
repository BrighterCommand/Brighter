using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandlerAsync : RequestHandlerAsync<MyRejectedEvent>
{
    public const string? TestOfRejectionFlow = "Test of rejection flow";

    public override Task<MyRejectedEvent> HandleAsync(MyRejectedEvent request, CancellationToken cancellationToken)
    {
        throw new RejectMessageAction(TestOfRejectionFlow);
    }
}
