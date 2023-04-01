using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class FakeExporter : BaseExporter<Activity>
{
    public List<Activity> ExportedActivities { get; } = new();

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var a in batch)
        {
            ExportedActivities.Add(a);
        }
        return ExportResult.Success;
    }
}
