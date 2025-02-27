using Paramore.Brighter;
using Paramore.Brighter.MessageScheduler.Quartz;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace ParamoreBrighter.Quartz.Tests.TestDoubles;

public class BrighterResolver : PropertySettingJobFactory
{
    public static IAmACommandProcessor Processor { get; set; }

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return new QuartzBrighterJob(Processor);
    }
}
