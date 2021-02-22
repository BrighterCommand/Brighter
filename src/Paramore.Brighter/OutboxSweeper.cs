using System.Linq;

namespace Paramore.Brighter
{
    public class OutboxSweeper
    {
        private readonly double _milliSecondsSinceSent;
        private readonly IAmAnOutboxViewer<Message> _outbox;
        private readonly IAmACommandProcessor _commandProcessor;

        /// <summary>
        /// This sweeper clears an outbox of any outstanding messages within the time interval
        /// </summary>
        /// <param name="milliSecondsSinceSent">How long can a message sit in the box before we attempt to resend</param>
        /// <param name="outbox">What is the outbox you want to check -- should be the same one supplied to the command processor below</param>
        /// <param name="commandProcessor">Who should post the messages</param>
        public OutboxSweeper(double milliSecondsSinceSent, IAmAnOutboxViewer<Message> outbox, IAmACommandProcessor commandProcessor)
        {
            _milliSecondsSinceSent = milliSecondsSinceSent;
            _outbox = outbox;
            _commandProcessor = commandProcessor;
        }

        public void Sweep()
        {
            //find all the unsent messages
            var outstandingMessages = _outbox.OutstandingMessages(_milliSecondsSinceSent);
           
            //send them
            _commandProcessor.ClearOutbox(outstandingMessages.Select(message => message.Id).ToArray());
           
        } 
    }
}
