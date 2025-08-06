#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Paramore.Brighter.FeatureSwitch;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Any pipeline has a request context that allows you to flow information between instances of <see cref="IHandleRequests"/>
    /// The default in-memory <see cref="RequestContext"/> created by an <see cref="InMemoryRequestContextFactory"/> is suitable for most purposes
    /// and this interface is mainly provided for testing
    /// </summary>
    public interface IRequestContext
    {
        /// <summary>
        /// Gets the Span [Activity] associated with the request
        /// </summary>
        Activity? Span { get; set; }
        
        /// <summary>
        /// The destination topic and cloud event type can be used to override the topic and cloud event type that the request is posted to.
        /// </summary>
        ProducerKey? Destination { get; set; }
        
        /// <summary>
        /// Gets the bag.
        /// </summary>
        /// <value>The bag.</value>
        ConcurrentDictionary<string, object> Bag { get; }
        
        /// <summary>
        /// [Obsolete] Gets the legacy policy registry.
        /// </summary>
        /// <value>
        /// The policy registry containing resilience policies. Returns <c>null</c> if no policies are configured.
        /// </value>
        /// <remarks>
        /// This property is obsolete and will be removed in a future version. 
        /// Migrate to <see cref="ResiliencePipeline"/> for new resilience implementations.
        /// </remarks> 
        [Obsolete("Migrate to ResiliencePipeline")]
        IPolicyRegistry<string>?  Policies { get; }
        
        /// <summary>
        /// Gets the registry of resilience pipelines.
        /// </summary>
        /// <value>
        /// The registry containing named resilience pipeline instances. Returns <c>null</c> if no pipelines are configured.
        /// </value>
        /// <remarks>
        /// Use this registry to retrieve pre-configured resilience pipelines by name. 
        /// This replaces the obsolete <see cref="Policies"/> property for modern resilience implementations.
        /// </remarks> 
        ResiliencePipelineRegistry<string>? ResiliencePipeline { get; }
        
        /// <summary>
        /// Gets the <see cref="ResilienceContext"/>
        /// </summary>
        ResilienceContext? ResilienceContext { get; }
        
        /// <summary>
        /// Gets the Feature Switches
        /// </summary>
        IAmAFeatureSwitchRegistry? FeatureSwitches { get; }
        
        /// <summary>
        /// Create a new copy of the Request Context
        /// </summary>
        /// <returns>a new copy of the request context</returns>
        IRequestContext CreateCopy();
    }
}
