namespace paramore.brighter.commandprocessor
{
    public class FallingBackEvent
    {
        public HandlerName ThisHandler { get; private set; }
        public HandlerName NextHandler { get; private set; }

        public FallingBackEvent(HandlerName thisHandler, HandlerName nextHandler)
        {
            ThisHandler = thisHandler;
            NextHandler = nextHandler;
        }
    }
}