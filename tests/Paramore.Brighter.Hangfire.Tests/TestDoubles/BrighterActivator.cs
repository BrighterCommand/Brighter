using Hangfire;
using Paramore.Brighter.MessageScheduler.Hangfire;

namespace Paramore.Brighter.Hangfire.Tests.TestDoubles;

public class BrighterActivator : JobActivator 
{
    private readonly IAmACommandProcessor _processor;

    public BrighterActivator(IAmACommandProcessor processor)
    {
        _processor = processor;
    }

    public override object ActivateJob(Type jobType)
    {
        if (jobType == typeof(BrighterHangfireSchedulerJob))
        {
            return new BrighterHangfireSchedulerJob(_processor);
        }
        
        return base.ActivateJob(jobType);
    }
}
