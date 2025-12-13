using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandlerAsync : RequestHandlerAsync<MyRejectedEvent>
{
    public override Task<MyRejectedEvent> HandleAsync(MyRejectedEvent request, CancellationToken cancellationToken)
    {
        throw new RejectMessageAction("Test of rejection flow");
    }
}
