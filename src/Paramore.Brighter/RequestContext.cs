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
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Carries execution metadata and runtime services between instances of <see cref="IHandleRequests"/> in a pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built-in request schedulers capture an independent <see cref="Scheduler.ScheduledRequestContext"/> snapshot
    /// when scheduling Send, Publish, or Post, including their asynchronous variants. Only dynamic headers,
    /// CloudEvents additional properties, the partition key, job/workflow/causation identifiers, and the supplied
    /// span's trace identifiers and baggage are captured. Job, workflow, and causation identifiers must be
    /// <see cref="Id"/> values; strings and <see cref="Guid"/> values under those bag keys are not captured.
    /// </para>
    /// <para>
    /// Header and CloudEvents values retain their types. Supported values are null, strings, characters,
    /// booleans, integral types, finite floating-point numbers, decimals, <see cref="Guid"/>, <see cref="DateTime"/>,
    /// <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>, <see cref="Uri"/>, and byte arrays. Dictionaries must
    /// use default string equality, <see cref="StringComparer.Ordinal"/>, or <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// Unsupported values, such as enums, arbitrary objects, other collections, delegates, and non-finite numbers,
    /// or unsupported key comparers cause a <see cref="System.Text.Json.JsonException"/> when scheduling.
    /// </para>
    /// <para>
    /// Other <see cref="Bag"/> entries and runtime properties, including <see cref="Destination"/>,
    /// <see cref="OriginatingMessage"/>, feature switches, and policy registries, are not captured.
    /// The original <see cref="Span"/> object is not serialized; a configured tracer starts a new span
    /// using the captured trace context when the request executes.
    /// </para>
    /// <para>
    /// Prefer UTC <see cref="DateTime"/> values or <see cref="DateTimeOffset"/> for metadata that crosses hosts.
    /// Local DateTime values can be converted to the executing host's time zone, changing their wall-clock fields.
    /// Keep <see cref="JsonConverters.JsonSerialisationOptions.Options"/> compatible between scheduling and execution:
    /// the enclosing snapshot and identifier fields use these options. Invalid stored context data fails
    /// execution before the request is dispatched; it is not replaced with an empty context.
    /// </para>
    /// <para>
    /// The serialized context adds to the scheduled payload size. Scheduler storage and transport size limits
    /// still apply and can cause scheduling to fail even when all metadata values are supported.
    /// Custom schedulers must implement <see cref="IAmARequestSchedulerSyncWithContext"/> or
    /// <see cref="IAmARequestSchedulerAsyncWithContext"/> to receive context; implementations of the original
    /// scheduler interfaces retain their existing behavior.
    /// </para>
    /// </remarks>
    public class RequestContext : IRequestContext
    {
        private readonly ConcurrentDictionary<int, Activity> _spans = new();

        public RequestContext() { }
        
        private RequestContext(ConcurrentDictionary<string, object> bag)
        {
            Bag = new ConcurrentDictionary<string, object>(bag);
        }

        /// <summary>
        /// The destination topic can be used to override the topic that the request is posted to.
        /// </summary>
        public ProducerKey? Destination { get; set; }

        /// <summary>
        /// Gets the bag.
        /// </summary>
        /// <value>The bag.</value>
        public ConcurrentDictionary<string, object> Bag { get; } = new();

        /// <summary>
        /// Gets the Feature Switches
        /// </summary>
        public IAmAFeatureSwitchRegistry? FeatureSwitches { get; set; }

        /// <summary>
        /// When we pass a requestContext through a receiver pipeline, we may want to pass the original message that started the pipeline.
        /// This is primarily useful for debugging - how did we get to this request?. But it is also useful for some request metadata that we
        /// do not want to transfer to the Request.
        /// This is not thread-safe; the assumption is that you set this from a single thread and access the message from multiple threads. It is
        /// not intended to be set from multiple threads.
        ///</summary>
        /// <value>The originating message</value> 
        public Message? OriginatingMessage { get; set; }

        /// <summary>
        /// [Obsolete] Gets or sets the legacy policy registry.
        /// </summary>
        /// <value>
        /// The policy registry containing resilience policies. Returns <c>null</c> if no policies are configured.
        /// </value>
        /// <remarks>
        /// This property is obsolete and will be removed in a future version. 
        /// Migrate to <see cref="ResiliencePipeline"/> for new resilience implementations.
        /// </remarks>  
        [Obsolete("Migrate to ResiliencePipeline")]
        public IPolicyRegistry<string>? Policies { get; set; }
        
        /// <summary>
        /// Gets or sets the registry of resilience pipelines.
        /// </summary>
        /// <value>
        /// The registry containing named resilience pipeline instances. Returns <c>null</c> if no pipelines are configured.
        /// </value>
        /// <remarks>
        /// Use this registry to retrieve pre-configured resilience pipelines by name. 
        /// This replaces the obsolete <see cref="Policies"/> property for modern resilience implementations.
        /// </remarks> 
        public ResiliencePipelineRegistry<string>? ResiliencePipeline { get; set; }

        /// <summary>
        /// Gets the <see cref="ResilienceContext"/> associated with Polly-based resilience operations.
        /// This context can be used to share data across different resilience policies during execution.
        /// </summary>
        public ResilienceContext? ResilienceContext { get; set; }

        /// <summary>
        /// Gets the Span [Activity] associated with the request
        /// This is thread-safe, so that you can access the context from multiple threads
        /// This is mainly required for Publish, which uses the same context across multiple Publish handlers
        /// </summary>
        public Activity? Span
        {
            get
            {
                _spans.TryGetValue(System.Threading.Thread.CurrentThread.ManagedThreadId, out var span);
                return span;
            }
            set
            {
                if(value is not null)
                    _spans.AddOrUpdate(System.Threading.Thread.CurrentThread.ManagedThreadId, value, (key, oldValue) => value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="InstrumentationOptions"/> that were configured for the pipeline that created this context.
        /// </summary>
        /// <remarks>
        /// This is the same value the <see cref="CommandProcessor"/> used when it created the <see cref="Span"/>, so middleware
        /// handlers can gate their own telemetry on it (for example on <see cref="InstrumentationOptions.Brighter"/>) without
        /// taking a dependency on how the processor was configured. Defaults to <see cref="InstrumentationOptions.All"/>.
        /// </remarks>
        public InstrumentationOptions InstrumentationOptions { get; set; } = InstrumentationOptions.All;

        /// <summary>
        /// Create a new instance of the Request Context
        /// </summary>
        /// <returns>New Instance of the message</returns>
        public IRequestContext CreateCopy()
            => new RequestContext(Bag)
            {
                Span = Span,
#pragma warning disable CS0618 // Type or member is obsolete
                Policies = Policies,
#pragma warning restore CS0618 // Type or member is obsolete
                ResiliencePipeline = ResiliencePipeline,
                FeatureSwitches = FeatureSwitches,
                OriginatingMessage = OriginatingMessage,
                InstrumentationOptions = InstrumentationOptions
            };
    }
}
