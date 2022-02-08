using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public class OutboxSweeper
    {
        private readonly IAmAnOutboxViewerAsync<Message> _outboxAsync = null;
        private readonly double _milliSecondsSinceSent;
        private readonly IAmAnOutboxViewer<Message> _outbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly int _batchSize;
        private readonly bool _useBulk;

        /// <summary>
        /// This sweeper clears an outbox of any outstanding messages within the time interval
        /// </summary>
        /// <param name="milliSecondsSinceSent">How long can a message sit in the box before we attempt to resend</param>
        /// <param name="outbox">What is the outbox you want to check -- should be the same one supplied to the command processor below</param>
        /// <param name="commandProcessor">Who should post the messages</param>
        /// <param name="batchSize">The maximum number of messages to dispatch.</param>
        /// <param name="useBulk">Use the producers bulk dispatch functionality.</param>
        public OutboxSweeper(double milliSecondsSinceSent, IAmAnOutboxViewer<Message> outbox,
            IAmACommandProcessor commandProcessor, int batchSize = 100, bool useBulk = false)
        {
            _milliSecondsSinceSent = milliSecondsSinceSent;
            _outbox = outbox;
            _commandProcessor = commandProcessor;
            _batchSize = batchSize;
            _useBulk = useBulk;

            if (outbox is IAmAnOutboxViewerAsync<Message> outboxViewerAsync) _outboxAsync = outboxViewerAsync;
        }

        public void Sweep()
        {
            Sweep(_milliSecondsSinceSent, _outbox, _commandProcessor);
        }

        public static void Sweep(double milliSecondsSinceSent, IAmAnOutboxViewer<Message> outbox, IAmACommandProcessor commandProcessor)
        {
            //find all the unsent messages
            var outstandingMessages = outbox.OutstandingMessages(milliSecondsSinceSent);
           
            //send them if we have them
            if (outstandingMessages.Any())
                commandProcessor.ClearOutbox(outstandingMessages.Select(message => message.Id).ToArray());
        }

        public async Task SweepAsync(CancellationToken cancellationToken = default)
        {
            if(_outboxAsync == null)
                throw new InvalidOperationException("No Async Outbox Viewer defined.");

            await SweepAsync(_milliSecondsSinceSent, _outboxAsync, _commandProcessor, cancellationToken, _batchSize, _useBulk);
        }
        
        /// <summary>
        /// Dispatch messages that have yet to be dispatched.
        /// </summary>
        /// <param name="milliSecondsSinceSent">Minimum age of messages to dispatch.</param>
        /// <param name="outbox">Outbox to dispatch from.</param>
        /// <param name="commandProcessor">Command Processor to dispatch to.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <param name="batchSize">The maximum number of messages to dispatch.</param>
        /// <param name="useBulk">Use the producers bulk dispatch functionality.</param>
        public static async Task SweepAsync(double milliSecondsSinceSent, IAmAnOutboxViewerAsync<Message> outbox, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken, int batchSize = 100, bool useBulk = false)
        {
            //find all the unsent messages
            var outstandingMessages = (await outbox.OutstandingMessagesAsync(milliSecondsSinceSent, pageSize: batchSize,
                cancellationToken: cancellationToken)).ToArray();
           
            //send them if we have them
            if (outstandingMessages.Any())
            {
                var messages = outstandingMessages.Select(message => message.Id).ToArray();
                if (useBulk)
                    await commandProcessor.BulkClearOutboxAsync(messages, cancellationToken: cancellationToken);
                else
                    await commandProcessor.ClearOutboxAsync(messages, cancellationToken: cancellationToken);
            }
        } 
    }
}
