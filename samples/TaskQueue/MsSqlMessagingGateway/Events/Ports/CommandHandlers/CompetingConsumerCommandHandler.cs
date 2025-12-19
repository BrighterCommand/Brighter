using System;
using Events.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.Actions;

namespace Events.Ports.CommandHandlers
{
    public class CompetingConsumerCommandHandler : RequestHandler<CompetingConsumerCommand>
    {
        private readonly IAmACommandCounter _commandCounter;
        private static readonly Random Generator = new Random();

        public CompetingConsumerCommandHandler(IAmACommandCounter commandCounter)
        {
            _commandCounter = commandCounter ?? throw new ArgumentNullException(nameof(commandCounter));
        }

        public override CompetingConsumerCommand Handle(CompetingConsumerCommand advanceTimerEvent)
        {
            try
            {
                // Let's simulate that some subscription is failing and retry later
                if (Generator.Next(100) % 10 == 0) throw new Exception("some exception occurred");
                Console.WriteLine($"command number {advanceTimerEvent.CommandNumber}");
                _commandCounter.CountCommand();
                return base.Handle(advanceTimerEvent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new DeferMessageAction();
            }
        }
    }
}
