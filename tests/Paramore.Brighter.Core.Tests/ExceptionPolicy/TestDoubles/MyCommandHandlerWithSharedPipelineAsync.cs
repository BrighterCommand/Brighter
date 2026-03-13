using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Policies.Attributes;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;

internal sealed class MyCommandHandlerWithSharedPipelineAsync : RequestHandlerAsync<MyCommand>
{
    public static bool ReceivedCommand { get; set; }

    [UseResiliencePipelineAsync("SharedRetryPolicy", 1)]
    public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
    {
        ReceivedCommand = true;
        return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }
}
