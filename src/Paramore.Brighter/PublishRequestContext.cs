#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter;

internal sealed class PublishRequestContext(IRequestContext context) : IRequestContext
{
    public ProducerKey? Destination
    {
        get => context.Destination;
        set => context.Destination = value;
    }

    public ConcurrentDictionary<string, object> Bag => context.Bag;

    public IAmAFeatureSwitchRegistry? FeatureSwitches => context.FeatureSwitches;

    public Message? OriginatingMessage
    {
        get => context.OriginatingMessage;
        set => context.OriginatingMessage = value;
    }

    [Obsolete("Migrate to ResiliencePipeline")]
    public IPolicyRegistry<string>? Policies => context.Policies;

    public ResiliencePipelineRegistry<string>? ResiliencePipeline => context.ResiliencePipeline;

    public ResilienceContext? ResilienceContext => null;

    public Activity? Span
    {
        get => context.Span;
        set => context.Span = value;
    }

    public InstrumentationOptions InstrumentationOptions
    {
        get => context.InstrumentationOptions;
        set => context.InstrumentationOptions = value;
    }

    public IRequestContext CreateCopy() => new PublishRequestContext(context.CreateCopy());
}
