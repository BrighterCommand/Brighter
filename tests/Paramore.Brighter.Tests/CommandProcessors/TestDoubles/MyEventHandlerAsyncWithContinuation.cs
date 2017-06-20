using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    class MyEventHandlerAsyncWithContinuation : RequestHandlerAsync<MyEvent>
    {
        private static MyEvent s_receivedEvent;

        public static int MonitorValue { get; set; }
        public static int WorkThreadId { get; set; }
        public static int ContinuationThreadId { get; set; }

        public MyEventHandlerAsyncWithContinuation()
        {
            s_receivedEvent = null;
        }

        public override async Task<MyEvent> HandleAsync(MyEvent command, CancellationToken cancellationToken = default(CancellationToken))
        {
            MonitorValue = 0;
            WorkThreadId = 0;
            ContinuationThreadId = 0;
            MonitorValue = 1;

            if (cancellationToken.IsCancellationRequested)
            {
                return command;
            }

            WorkThreadId = Thread.CurrentThread.ManagedThreadId;

            LogEvent(command);
            await Task.Delay(30, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            //this will run on the continuation
            MonitorValue = 2;
            ContinuationThreadId = Thread.CurrentThread.ManagedThreadId;

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
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
