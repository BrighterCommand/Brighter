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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class BrighterOptions : IBrighterOptions
    {
        /// <summary>
        /// Do we support feature switching? In which case please supply an initialized feature switch registry
        /// </summary>
        /// <returns></returns>
        public IAmAFeatureSwitchRegistry? FeatureSwitchRegistry { get; set; } = null;

        /// <summary>
        /// Configures the lifetime of the Handlers. Defaults to Transient.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Controls the DI-scope granularity of <see cref="ServiceLifetime.Transient"/> handlers only.
        /// When <c>true</c> (the default) each transient handler resolution in a pipeline gets its own DI
        /// scope, so a scoped-registered dependency is a distinct instance per handler. When <c>false</c>
        /// the transient handlers in one request pipeline share a single DI scope that is disposed when the
        /// pipeline completes — the pre-#4254 behaviour, where a scoped dependency is one shared instance
        /// across the whole chain. Has no effect on Scoped or Singleton handlers (a Scoped handler already
        /// shares the pipeline scope by definition), nor on mapper/transformer factories (which always
        /// isolate — that is the #4252 leak fix).
        /// <para>
        /// To share state across a pipeline, prefer <see cref="HandlerLifetime"/> = <c>Scoped</c>; this flag
        /// is a compatibility fallback for code that relied on the pre-#4254 sharing under <c>Transient</c>
        /// and cannot switch to <c>Scoped</c> yet.
        /// </para>
        /// </summary>
        public bool IsolateTransientHandlerScope { get; set; } = true;

        /// <summary>
        /// Configures how verbose our instrumentation is
        /// InstrumentationOptions.None - no instrumentation
        /// InstrumentationOptions.RequestInformation - just the request id, request type and operation
        /// InstrumentationOptions.RequestBody - the request body
        /// InstrumentationOptions.RequestContext - the request context
        /// InstrumentationOptions.All - all of the above
        /// </summary>
        public InstrumentationOptions InstrumentationOptions { get; set; }

        /// <summary>
        /// Configures the lifetime of mappers. Defaults to transit 
        /// </summary>
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Transient;

        /// <inheritdoc />
        [Obsolete("Migrate to ResiliencePipeline")]
        public IPolicyRegistry<string>? PolicyRegistry { get; set; } = new DefaultPolicy();

        /// <inheritdoc />
        public ResiliencePipelineRegistry<string>? ResiliencePipelineRegistry { get; set; }

        /// <summary>
        /// Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        public IAmARequestContextFactory RequestContextFactory { get; set; } = new InMemoryRequestContextFactory();

        /// <summary>
        /// Configures the lifetime of any transformers. Defaults to Transient
        /// </summary>
        public ServiceLifetime TransformerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Whether a pipeline should adopt an ambient DI scope its caller already owns, or always
        /// create and own its own. Defaults to <see cref="ScopeAffinity.AlwaysNew"/>. Applies to
        /// <see cref="ServiceLifetime.Scoped"/> handler, mapper and transformer lifetimes only.
        /// </summary>
        public ScopeAffinity DefaultScopeAffinity { get; set; } = ScopeAffinity.AlwaysNew;
    }

    public interface IBrighterOptions
    {
        /// <summary>
        /// Do we support feature switching? In which case please supply an initialized feature switch registry
        /// </summary>
        /// <returns></returns>
        IAmAFeatureSwitchRegistry? FeatureSwitchRegistry { get; set; }
        
         /// <summary>
        /// Configures the lifetime of the Handlers.
        /// </summary>
        ServiceLifetime HandlerLifetime { get; set; }

        /// <summary>
        /// Controls the DI-scope granularity of <see cref="ServiceLifetime.Transient"/> handlers only.
        /// When <c>true</c> (the default) each transient handler resolution in a pipeline gets its own DI
        /// scope; when <c>false</c> the transient handlers in one request pipeline share a single DI scope
        /// (the pre-#4254 behaviour). No effect on Scoped/Singleton handlers or on mapper/transformer
        /// factories. To share state across a pipeline prefer <see cref="HandlerLifetime"/> = <c>Scoped</c>;
        /// this flag is a compatibility fallback, not a knob most applications should touch.
        /// </summary>
        bool IsolateTransientHandlerScope { get; set; }
         
        /// <summary>
        /// What depth of instrumentation do we need
        /// InstrumentationOptions.None - no instrumentation
        /// InstrumentationOptions.RequestInformation - just the request id, request type and operation
        /// InstrumentationOptions.RequestBody - the request body
        /// InstrumentationOptions.RequestContext - the request context
        /// InstrumentationOptions.All - all of the above
        /// </summary> 
        InstrumentationOptions InstrumentationOptions { get; set; } 

        /// <summary>
        /// Configures the lifetime of mappers. 
        /// </summary>
        ServiceLifetime MapperLifetime { get; set; }

        /// <summary>
        /// [Obsolete] Configure the legacy policy registry.
        /// </summary>
        /// <value>
        /// The policy registry containing resilience policies. Returns <c>null</c> if no policies are configured.
        /// </value>
        /// <remarks>
        /// This property is obsolete and will be removed in a future version. 
        /// Migrate to <see cref="ResiliencePipelineRegistry"/> for new resilience implementations.
        /// </remarks>
        [Obsolete("Migrate to ResiliencePipeline", error: false)]
        IPolicyRegistry<string>? PolicyRegistry { get; set; }
        
        /// <summary>
        /// Configures the registry of resilience pipelines shared by handlers and outbox producers.
        /// </summary>
        /// <value>
        /// The registry containing named resilience pipeline instances. Returns <c>null</c> if no pipelines are configured.
        /// </value>
        /// <remarks>
        /// The registry is resolved when Brighter services are first requested, after deferred options configuration.
        /// An explicit <see cref="IBrighterBuilder.ResiliencePolicyRegistry"/> overrides this value.
        /// If neither entry point supplies a registry, Brighter creates one.
        /// Brighter adds missing default pipelines to the selected registry without replacing configured pipelines.
        /// This replaces the obsolete <see cref="PolicyRegistry"/> property for modern resilience implementations.
        /// </remarks>
        ResiliencePipelineRegistry<string>? ResiliencePipelineRegistry { get; set; }

        /// <summary>
        ///     Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        IAmARequestContextFactory RequestContextFactory { get; set; }
        
        /// <summary>
        /// Configures the lifetime of any transformers.
        /// </summary>
        ServiceLifetime TransformerLifetime { get; set; }

        /// <summary>
        /// Whether a pipeline should adopt an ambient DI scope its caller already owns, or always
        /// create and own its own. Applies to <see cref="ServiceLifetime.Scoped"/> handler, mapper
        /// and transformer lifetimes only.
        /// </summary>
        ScopeAffinity DefaultScopeAffinity { get; set; }

   }
}
