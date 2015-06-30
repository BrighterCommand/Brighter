namespace paramore.brighter.commandprocessor
{
    public class PipelineContinuingEvent
    {
        public HandlerName ThisHandler { get; private set; }
        public HandlerName NextHandler { get; private set; }

        public PipelineContinuingEvent(HandlerName thisHandler, HandlerName nextHandler)
        {
            ThisHandler = thisHandler;
            NextHandler = nextHandler;
        }
    }
}