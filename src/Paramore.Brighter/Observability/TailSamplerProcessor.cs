using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Paramore.Brighter.Observability;

/// <summary>
/// A processor that delegates sampling decisions to an OpenTelemetry Sampler.
/// This allows for using sampling logic after the span is completed.
/// Provided to ease adoption of BrighterMetricsFromTracesProcessor
/// </summary>
public sealed class TailSamplerProcessor(Sampler sampler)
    : BaseProcessor<Activity>
{
    private readonly Sampler _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));

    public override void OnEnd(Activity? activity)
    {
        if (activity == null) return;

        var samplingParameters = new SamplingParameters(
            parentContext: activity.Parent?.Context ?? default,
            traceId: activity.TraceId,
            name: activity.OperationName,
            kind: activity.Kind,
            tags: activity.TagObjects,
            links: activity.Links);

        var result = _sampler.ShouldSample(samplingParameters);
        
        if (result.Decision is not SamplingDecision.RecordAndSample)
        {
            // exporters check this value to determine if the span should be exported
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            activity.IsAllDataRequested = false;
        }

        base.OnEnd(activity);
    }
}