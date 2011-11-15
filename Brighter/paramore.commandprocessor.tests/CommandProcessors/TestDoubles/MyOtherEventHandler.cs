namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyOtherEventHandler : RequestHandler<MyEvent>
    {
        private static MyEvent receivedEvent;

        public MyOtherEventHandler()
        {
            receivedEvent = null;
        }

        public override MyEvent Handle(MyEvent command)
        {
            LogEvent(command);
            return command;
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
