using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Policies.Attributes;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;

internal sealed class MyOtherCommandHandlerWithSharedPipelineAsync : RequestHandlerAsync<MyOtherCommand>
{
    public static bool ReceivedCommand { get; set; }

    [UseResiliencePipelineAsync("SharedRetryPolicy", 1)]
    public override async Task<MyOtherCommand> HandleAsync(MyOtherCommand command, CancellationToken cancellationToken = default)
    {
        ReceivedCommand = true;
        return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }
}
