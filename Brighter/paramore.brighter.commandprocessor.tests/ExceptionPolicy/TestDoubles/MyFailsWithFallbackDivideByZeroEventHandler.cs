using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Attributes;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles
{
    internal class MyFailsWithFallbackDivideByZeroEventHandler : RequestHandler<MyEvent>
    {
        public MyFailsWithFallbackDivideByZeroEventHandler (ILog logger) : base(logger)
        { }

        public static bool FallbackCalled { get; set; }
        public static bool ReceivedCommand { get; set; }
        public static bool SetException { get; set; }

        static MyFailsWithFallbackDivideByZeroEventHandler()
        {
            ReceivedCommand = false;
        }

        [FallbackPolicy(backstop: true, circuitBreaker: false, step: 1)]
        public override MyEvent Handle(MyEvent command)
        {
            ReceivedCommand = true;
            throw new DivideByZeroException();
        }

        public override MyEvent Fallback(MyEvent command)
        {
            FallbackCalled = true;
            if (Context.Bag.ContainsKey(FallbackPolicyHandler<MyCommand>.CAUSE_OF_FALLBACK_EXCEPTION))
                SetException = true;
            return base.Fallback(command);
        }

        public static bool ShouldFallback(MyEvent command)
        {
            return FallbackCalled;
        }

        public static bool ShouldReceive(MyEvent myCommand)
        {
            return ReceivedCommand;
        }

        public static bool ShouldSetException(MyEvent myCommand)
        {
            return SetException;
        }
    }
}