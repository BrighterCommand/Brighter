using Paramore.Brighter;
using Paramore.Brighter.MessageScheduler.Quartz;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace ParamoreBrighter.Quartz.Tests.TestDoubles;

public class BrighterResolver : PropertySettingJobFactory
{
    public const string ProcessorContextKey = "BrighterProcessor";

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var processor = scheduler.Context[ProcessorContextKey] as IAmACommandProcessor
            ?? throw new InvalidOperationException("Quartz scheduler context is missing the Brighter command processor.");

        return new QuartzBrighterJob(processor);
    }
}
