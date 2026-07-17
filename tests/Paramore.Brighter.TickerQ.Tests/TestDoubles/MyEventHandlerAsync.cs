using System.Collections.Concurrent;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class MyEventHandlerAsync(ConcurrentDictionary<string, string> receivedMessages) : RequestHandlerAsync<MyEvent>
{
    public override async Task<MyEvent> HandleAsync(MyEvent command, CancellationToken cancellationToken = default)
    {
        receivedMessages[command.Id] = nameof(MyEventHandlerAsync);
        return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }
}
