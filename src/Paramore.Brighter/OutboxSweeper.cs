namespace Paramore.Brighter
{
    public class OutboxSweeper
    {
        private readonly int _millisecondsSinceSent;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly int _batchSize;
        private readonly bool _useBulk;

        /// <summary>
        /// This sweeper clears an outbox of any outstanding messages within the time interval
        /// </summary>
        /// <param name="millisecondsSinceSent">How long can a message sit in the box before we attempt to resend</param>
        /// <param name="commandProcessor">Who should post the messages</param>
        /// <param name="batchSize">The maximum number of messages to dispatch.</param>
        /// <param name="useBulk">Use the producers bulk dispatch functionality.</param>
        public OutboxSweeper(int millisecondsSinceSent, IAmACommandProcessor commandProcessor, int batchSize = 100,
            bool useBulk = false)
        {
            _millisecondsSinceSent = millisecondsSinceSent;
            _commandProcessor = commandProcessor;
            _batchSize = batchSize;
            _useBulk = useBulk;
        }

        /// <summary>
        /// Dispatches the oldest un-dispatched messages from the outbox in a background thread.
        /// </summary>
        public void Sweep()
        {
            _commandProcessor.ClearOutbox(_batchSize, _millisecondsSinceSent);
        }

        /// <summary>
        /// Dispatches the oldest un-dispatched messages from the asynchronous outbox in a background thread.
        /// </summary>
        public void SweepAsyncOutbox()
        {
            _commandProcessor.ClearAsyncOutbox(_batchSize, _millisecondsSinceSent, _useBulk);
        }
    }
}
