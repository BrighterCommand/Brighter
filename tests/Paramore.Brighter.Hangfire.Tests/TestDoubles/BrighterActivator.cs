using Hangfire;
using Paramore.Brighter.MessageScheduler.Hangfire;

namespace Paramore.Brighter.Hangfire.Tests.TestDoubles;

public class BrighterActivator : JobActivator 
{
    public static IAmACommandProcessor Processor { get; set; }

    public override object ActivateJob(Type jobType)
    {
        if (jobType == typeof(BrighterHangfireSchedulerJob))
        {
            return new BrighterHangfireSchedulerJob(Processor);
        }
        
        return base.ActivateJob(jobType);
    }
}
