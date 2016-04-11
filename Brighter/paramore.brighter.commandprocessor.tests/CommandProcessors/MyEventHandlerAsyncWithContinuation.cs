using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    class MyEventHandlerAsyncWithContinuation : RequestHandlerAsync<MyEvent>
    {
        private static MyEvent s_receivedEvent;
        private int counter = 0;

        public static ThreadLocal<int> LoopCounter { get; set; }
        public static int WorkThreadId { get; set; }
        public static int ContinuationThreadId { get; set; }


        public MyEventHandlerAsyncWithContinuation(ILog logger) : base(logger)
        {
            s_receivedEvent = null;
        }

        public override async Task<MyEvent> HandleAsync(MyEvent command, CancellationToken? ct = null)
        {
            LoopCounter = new ThreadLocal<int>(() => 0);
            WorkThreadId = 0;
            ContinuationThreadId = 0;
            LoopCounter.Value = 1;

            if (ct.HasValue && ct.Value.IsCancellationRequested)
            {
                return command;
            }

            WorkThreadId = Thread.CurrentThread.ManagedThreadId;

            LogEvent(command);
            await Task.Delay(0);

            //this will run on the continuation
            LoopCounter.Value = 2;
            ContinuationThreadId = Thread.CurrentThread.ManagedThreadId;
            
            return command;
        }

        private static void LogEvent(MyEvent @event)
        {
            s_receivedEvent = @event;
        }

        public static bool ShouldReceive(MyEvent myEvent)
        {
            return s_receivedEvent.Id == myEvent.Id;
        }
    }
}
