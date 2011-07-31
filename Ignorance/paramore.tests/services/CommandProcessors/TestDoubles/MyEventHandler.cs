using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyEventHandler : RequestHandler<MyEvent>
    {
        private static MyEvent receivedEvent;


        public MyEventHandler()
        {
            receivedEvent = null;
        }

        public override MyEvent Handle(MyEvent @event)
        {
            LogEvent(@event);
            return @event;
        }

        private static void LogEvent(MyEvent @event)
        {
            receivedEvent = @event;
        }

        public static bool ShouldRecieve(MyEvent myEvent)
        {
            return receivedEvent.Id == myEvent.Id;
        }
    }
}