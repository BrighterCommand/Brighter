using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    internal sealed class FailingMessageProducer(IAmABus bus, TimeProvider timeProvider, InstrumentationOptions instrumentationOptions)
            : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
    {
        private ITimer? _requeueTimer;

        public Publication Publication { get; set; } = new();

        public Activity? Span { get; set; }
        public IAmAMessageScheduler? Scheduler { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
        }

        public Task SendAsync(Message message, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<Id[]> SendAsync(
            IEnumerable<Message> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken
            )
            => throw new NotImplementedException();
        public void Send(Message message)
            => throw new NotImplementedException();
        public void SendWithDelay(Message message, TimeSpan? delay = null)
            => throw new NotImplementedException();
        public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default) 
            => throw new NotImplementedException();
        public Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public ValueTask<IEnumerable<IAmAMessageBatch>> CreateBatchesAsync(IEnumerable<Message> messages,
            CancellationToken cancellationToken)
            => new([new MessageBatch(messages)]);
    }
}
