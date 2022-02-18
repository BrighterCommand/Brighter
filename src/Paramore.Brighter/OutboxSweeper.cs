using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public class OutboxSweeper
    {
        private readonly int _milliSecondsSinceSent;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly int _batchSize;
        private readonly bool _useBulk;

        /// <summary>
        /// This sweeper clears an outbox of any outstanding messages within the time interval
        /// </summary>
        /// <param name="milliSecondsSinceSent">How long can a message sit in the box before we attempt to resend</param>
        /// <param name="commandProcessor">Who should post the messages</param>
        /// <param name="batchSize">The maximum number of messages to dispatch.</param>
        /// <param name="useBulk">Use the producers bulk dispatch functionality.</param>
        public OutboxSweeper(int milliSecondsSinceSent, IAmACommandProcessor commandProcessor, int batchSize = 100,
            bool useBulk = false)
        {
            _milliSecondsSinceSent = milliSecondsSinceSent;
            _commandProcessor = commandProcessor;
            _batchSize = batchSize;
            _useBulk = useBulk;
        }

        public void Sweep()
        {
            _commandProcessor.ClearOutbox(_batchSize, _milliSecondsSinceSent);
        }

        public Task SweepAsync(CancellationToken cancellationToken = default)
        {
            _commandProcessor.ClearAsyncOutbox(_batchSize, _milliSecondsSinceSent, _useBulk);
            
            return Task.CompletedTask;
        }

        public void SweepAsyncOutbox()
        {
            _commandProcessor.ClearAsyncOutbox(_batchSize, _milliSecondsSinceSent, _useBulk);
        }
    }
}
