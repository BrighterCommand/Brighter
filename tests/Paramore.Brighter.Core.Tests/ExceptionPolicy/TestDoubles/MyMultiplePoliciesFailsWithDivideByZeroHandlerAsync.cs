using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Policies.Attributes;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync : RequestHandlerAsync<MyCommand>
{
    public static bool ReceivedCommand { get; set; }

    public MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync()
    {
        ReceivedCommand = false;
    }

    [UsePolicyAsync(new [] {"MyDivideByZeroBreakerPolicyAsync", "MyDivideByZeroRetryPolicyAsync", }, 1)]
    public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
    {
        ReceivedCommand = true;
        throw new DivideByZeroException();
    }

    public static bool ShouldReceive(MyCommand myCommand)
    {
        return ReceivedCommand;
    }
    
}
