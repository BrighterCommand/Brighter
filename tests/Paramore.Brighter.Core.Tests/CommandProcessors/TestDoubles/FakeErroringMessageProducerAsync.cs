using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeErroringMessageProducerAsync : IAmAMessageProducerAsync
    {
        public int SentCalledCount { get; private set; }
        public Publication Publication { get; } = new();

        public Activity Span { get; set; }
        public IAmAMessageScheduler Scheduler { get; set; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            SentCalledCount++;
            throw new InvalidOperationException("The broker is unavailable");
        }

        public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            return SendAsync(message, cancellationToken);
        }
    }
}
