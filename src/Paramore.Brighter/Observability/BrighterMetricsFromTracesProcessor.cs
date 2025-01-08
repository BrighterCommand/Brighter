#region Licence

/* The MIT License (MIT)
Copyright © 2025 Tim Salva <tim@jtsalva.dev>

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

using System.Diagnostics;
using OpenTelemetry;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Generates metrics from traces following OpenTelemetry Semantic Conventions 1.29.0
/// Unstable due to depending on experimental parts of the OpenTelemetry spec
/// </summary>
public sealed class BrighterMetricsFromTracesProcessor(
    IAmABrighterTracer brighterTracer,
    IAmABrighterDbMeter dbMeter,
    IAmABrighterMessagingMeter messagingMeter)
    : BaseProcessor<Activity>
{
    private readonly string _brighterActivitySourceName = brighterTracer.ActivitySource.Name;
    
    private bool Enabled => dbMeter.Enabled || messagingMeter.Enabled;

    public override void OnEnd(Activity? activity)
    {
        if (!Enabled) return;
        
        if (activity == null) return;
        
        if (!activity.Source.Name.Equals(_brighterActivitySourceName)) return;
        
        if (activity.GetTagItem(BrighterSemanticConventions.InstrumentationDomain) is not string instrumentationDomain) return;

        switch (instrumentationDomain)
        {
            case BrighterSemanticConventions.MessagingInstrumentationDomain:
                if (activity.GetTagItem(BrighterSemanticConventions.MessagingOperationType) is string operation)
                {
                    switch (operation)
                    {
                        case "publish":
                            messagingMeter.RecordClientOperation(activity);
                            messagingMeter.AddClientSentMessage(activity);
                            break;
                        case "receive":
                            messagingMeter.RecordClientOperation(activity);
                            messagingMeter.AddClientConsumedMessage(activity);
                            break;
                        case "process":
                            messagingMeter.RecordProcess(activity);
                            break;
                        default:
                            messagingMeter.RecordClientOperation(activity);
                            break;
                    }
                }
                break;
            case BrighterSemanticConventions.DbInstrumentationDomain:
                dbMeter.RecordClientOperation(activity);
                break;
        }

        base.OnEnd(activity);
    }
}
