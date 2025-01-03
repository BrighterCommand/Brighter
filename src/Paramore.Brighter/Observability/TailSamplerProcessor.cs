#region Licence

/* The MIT License (MIT)
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

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
            name: activity.DisplayName,
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