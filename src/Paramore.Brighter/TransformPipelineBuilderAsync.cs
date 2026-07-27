#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// We use attributes to allow you to mix in behaviours when message mapping such as offloading large payloads via a claim check or
    /// encrypting PII content. Because CSharp has single-inheritance, we can't build these in a re-usable fashion via inheritance, and
    /// instead of forcing you to use DI to pull in those behaviors, we offer the option to use an attribute to mark up your message transform
    /// with suitable changes.
    /// We run a <see cref="WrapWithAttribute"/> after the message mapper converts from an <see cref="IRequest"/>.
    /// We run a <see cref="UnwrapWithAttribute"/> before the message mapper converts to a <see cref="IRequest"/>.
    /// You handle translation between <see cref="IRequest"/> and <see cref="Message"/> in your <see cref="IAmAMessageMapper{TRequest}"/>
    /// </summary>
    public partial class TransformPipelineBuilderAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TransformPipelineBuilder>();
        private readonly IAmAMessageMapperRegistryAsync _mapperRegistryAsync;

        private readonly IAmAMessageTransformerFactoryAsync _messageTransformerFactoryAsync;
        private readonly InstrumentationOptions _instrumentationOptions;

        //GLOBAL! Cache of message mapper transform attributes. This will not be recalculated post start up. Method to clear cache below (if a broken test brought you here).
        private static readonly ConcurrentDictionary<Type, IOrderedEnumerable<WrapWithAttribute>> s_wrapTransformsMemento =
            new ConcurrentDictionary<Type, IOrderedEnumerable<WrapWithAttribute>>();

        private static readonly ConcurrentDictionary<Type, IOrderedEnumerable<UnwrapWithAttribute>> s_unWrapTransformsMemento =
            new ConcurrentDictionary<Type, IOrderedEnumerable<UnwrapWithAttribute>>();

        /// <summary>
        /// Creates an instance of a transform pipeline builder.
        /// To avoid introducing a breaking interface for v9 we allow the transform factory to be optional,
        /// so that it can be optional in a CommandProcessor constructor, and not make a breaking change to the interface.
        /// In this case, transform pipelines mimic v9 behaviour and just run the mapper  and not any transforms
        /// To avoid silent failure, we warn on this.
        /// </summary>
        /// <param name="mapperRegistryAsync">The async message mapper registry, cannot be null</param>
        /// <param name="messageTransformerFactoryAsync">The async transform factory, can be null</param>
        /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
        /// <exception cref="ConfigurationException">Throws a configuration exception on a null mapperRegistry</exception>
        public TransformPipelineBuilderAsync(
            IAmAMessageMapperRegistryAsync mapperRegistryAsync,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync,
            InstrumentationOptions instrumentationOptions
        )
        {
            _mapperRegistryAsync = mapperRegistryAsync ??
                                   throw new ConfigurationException(
                                       "TransformPipelineBuilder expected a Message Mapper Registry but none supplied");
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync;
            _instrumentationOptions = instrumentationOptions;
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref="WrapWithAttribute"/> will run before the <see cref="IAmAMessageMapper{TRequest}"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public WrapPipelineAsync<TRequest> BuildWrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            IAmAMessageMapperAsync<TRequest>? messageMapper = null;
            IEnumerable<IAmAMessageTransformAsync>? transforms = null;
            WrapPipelineAsync<TRequest>? pipeline = null;
            try
            {
                messageMapper = FindMessageMapper<TRequest>();

                transforms = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapper));

                pipeline = new WrapPipelineAsync<TRequest>(messageMapper, _messageTransformerFactoryAsync, transforms, _instrumentationOptions, _mapperRegistryAsync);

                Log.NewWrapPipelineCreated(s_logger, typeof(TRequest).Name, TraceWrapPipeline(pipeline));

                var unwraps = FindUnwrapTransforms(messageMapper);
                if (unwraps.Any())
                {
                    Log.UnwrapAttributesOnMapToMessageMethodIgnored(s_logger, typeof(TRequest).Name, TraceWrapPipeline(pipeline));
                }

                return pipeline;
            }
            catch (Exception e)
            {
                //nothing was returned to the caller to take ownership of the mapper and transforms, so
                //release them here rather than leak them. Cleanup may throw (Release/Dispose surface
                //exceptions), so guard it: a disposal failure must not mask the configuration error
                //the caller needs to see.
                try { CleanUpAfterFailedBuild(pipeline, transforms, messageMapper); }
                catch { /* swallowed: cleanup is best-effort; do not mask the build error rethrown below */ }
                throw new ConfigurationException("Error building wrap pipeline for outgoing message, see inner exception for details", e);
            }
        }

        /// <summary>
        /// Builds a pipeline.
        /// Anything marked with <see cref="UnwrapWithAttribute"/> will run after the <see cref="IAmAMessageMapper{TRequest}"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public UnwrapPipelineAsync<TRequest> BuildUnwrapPipeline<TRequest>() where TRequest : class, IRequest
        {
            IAmAMessageMapperAsync<TRequest>? messageMapper = null;
            IEnumerable<IAmAMessageTransformAsync>? transforms = null;
            UnwrapPipelineAsync<TRequest>? pipeline = null;
            try
            {
                messageMapper = FindMessageMapper<TRequest>();

                transforms = BuildTransformPipeline<TRequest>(FindUnwrapTransforms(messageMapper));

                pipeline = new UnwrapPipelineAsync<TRequest>(transforms, _messageTransformerFactoryAsync, messageMapper, _mapperRegistryAsync);

                Log.NewUnwrapPipelineCreated(s_logger, typeof(TRequest).Name, TraceUnwrapPipeline(pipeline));

                var wraps = FindWrapTransforms(messageMapper);
                if (wraps.Any())
                {
                    Log.WrapAttributesOnMapToRequestMethodIgnored(s_logger, typeof(TRequest).Name, TraceUnwrapPipeline(pipeline));
                }

                return pipeline;
            }
            catch (Exception e)
            {
                //nothing was returned to the caller to take ownership of the mapper and transforms, so
                //release them here rather than leak them. Cleanup may throw (Release/Dispose surface
                //exceptions), so guard it: a disposal failure must not mask the configuration error
                //the caller needs to see.
                try { CleanUpAfterFailedBuild(pipeline, transforms, messageMapper); }
                catch { /* swallowed: cleanup is best-effort; do not mask the build error rethrown below */ }
                throw new ConfigurationException("Error building unwrap pipeline for outgoing message, see inner exception for details", e);
            }
        }
        
        public bool HasPipeline<TRequest>() where TRequest : class, IRequest
        {
            //GetAsync creates an instance to answer the question, so release it again; no pipeline owns it.
            //Synchronous release: this overload exists for the sync-over-async reply path
            //(OutboxProducerMediator.CreateRequestFromMessage), which cannot await. Prefer HasPipelineAsync
            //from an async caller so the probe mapper is released without blocking the pump.
            var messageMapper = _mapperRegistryAsync.GetAsync<TRequest>();
            try
            {
                return messageMapper is not null;
            }
            finally
            {
                //Releasing the probe mapper is best-effort: a throwing release must not propagate out of a
                //predicate and be mistaken for a mapping failure by the caller. Log and swallow.
                if (messageMapper is not null)
                {
                    try
                    {
                        _mapperRegistryAsync.Release(messageMapper);
                    }
                    catch (Exception releaseException)
                    {
                        Log.FailedToReleaseProbeMapper(s_logger, releaseException, typeof(TRequest).Name);
                    }
                }
            }
        }

        public async ValueTask<bool> HasPipelineAsync<TRequest>() where TRequest : class, IRequest
        {
            //GetAsync creates an instance to answer the question, so release it again; no pipeline owns it.
            //Release asynchronously so that, driven from the Proactor pump, an IAsyncDisposable probe mapper
            //is awaited rather than drained through a blocking wait — the stall the synchronous HasPipeline
            //pays on that thread.
            var messageMapper = _mapperRegistryAsync.GetAsync<TRequest>();
            try
            {
                return messageMapper is not null;
            }
            finally
            {
                //Releasing the probe mapper is best-effort: a throwing release must not propagate out of a
                //predicate and be mistaken for a mapping failure by the caller. Log and swallow.
                if (messageMapper is not null)
                {
                    try
                    {
                        await _mapperRegistryAsync.ReleaseAsync(messageMapper).ConfigureAwait(false);
                    }
                    catch (Exception releaseException)
                    {
                        Log.FailedToReleaseProbeMapper(s_logger, releaseException, typeof(TRequest).Name);
                    }
                }
            }
        }

        private IEnumerable<IAmAMessageTransformAsync> BuildTransformPipeline<TRequest>(IEnumerable<TransformAttribute> transformAttributes)
            where TRequest : class, IRequest
        {
            var transforms = new List<IAmAMessageTransformAsync>();

            //Allowed to be null to avoid breaking v9 interfaces
            if (_messageTransformerFactoryAsync == null)
            {
                int i = transformAttributes.Count();
                if (i > 0)
                    Log.NoMessageTransformerFactoryConfigured(s_logger, i);

                return transforms;
            }

            try
            {
                transformAttributes.Each((attribute) =>
                {
                    var transformType = attribute.GetHandlerType();
                    var transformer = new TransformerFactoryAsync<TRequest>(attribute, _messageTransformerFactoryAsync).CreateMessageTransformer();
                    if (transformer == null)
                    {
                        throw new InvalidOperationException($"Message Transformer Factory could not create a transform of type {transformType.Name}");
                    }

                    transforms.Add(transformer);
                });
            }
            catch (Exception)
            {
                //a transform later in the pipeline failed to build; we own every transform created
                //before it, so release them rather than leak them before the error propagates. No
                //pipeline was constructed to take ownership of them.
                ReleaseTransforms(transforms);
                throw;
            }

            return transforms;
        }

        //Releases transforms back to the factory. Used to clean up a partially-built pipeline; a no-op
        //when no transformer factory was supplied (v9 compatibility), because none were created.
        private void ReleaseTransforms(IEnumerable<IAmAMessageTransformAsync> transforms)
        {
            if (_messageTransformerFactoryAsync is null) return;

            //Drain a materialised copy, releasing every transform even when one Release throws. This is the
            //failed-build path: no pipeline was constructed to own these transforms and no finalizer will
            //ever retry them (unlike TransformLifetimeScopeAsync, whose drain relies on a retry), so a
            //Release that skipped the rest would leak their DI scopes permanently. Removing each before
            //releasing keeps the list truthful, and swallowing a per-transform failure both keeps a later
            //transform from being skipped and keeps the original build error — rethrown by the caller —
            //from being masked by a disposal failure.
            var remaining = transforms.ToList();
            while (remaining.Count > 0)
            {
                var transform = remaining[0];
                remaining.RemoveAt(0);
                try { _messageTransformerFactoryAsync.Release(transform); }
                catch { /* swallowed: best-effort cleanup must not skip the rest or mask the build error */ }
            }
        }

        //Releases the resources created for a pipeline whose build failed before it was returned to the
        //caller. If the pipeline was constructed it owns the mapper and transforms, so disposing it
        //releases both exactly once (and suppresses its finalizer); otherwise we release whatever we
        //built directly. BuildTransformPipeline releases its own partial list when it throws, so
        //transforms is only non-null here when it returned successfully.
        private void CleanUpAfterFailedBuild<TRequest>(
            TransformPipelineAsync<TRequest>? pipeline,
            IEnumerable<IAmAMessageTransformAsync>? transforms,
            IAmAMessageMapperAsync<TRequest>? messageMapper)
            where TRequest : class, IRequest
        {
            if (pipeline is not null)
            {
                pipeline.Dispose();
                return;
            }

            if (transforms is not null) ReleaseTransforms(transforms);
            if (messageMapper is not null) _mapperRegistryAsync.Release(messageMapper);
        }

        public static void ClearPipelineCache()
        {
            s_wrapTransformsMemento.Clear();
            s_unWrapTransformsMemento.Clear();
        }

        private IAmAMessageMapperAsync<TRequest> FindMessageMapper<TRequest>() where TRequest : class, IRequest
        {
            var messageMapper = _mapperRegistryAsync.GetAsync<TRequest>();
            if (messageMapper == null) throw new InvalidOperationException($"Could not find mapper for {typeof(TRequest).Name}. Hint: did you set MessagePumpType.Proactor on the subscription to match the mapper type?");
            return messageMapper;
        }

        private IOrderedEnumerable<WrapWithAttribute> FindWrapTransforms<T>(IAmAMessageMapperAsync<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType();
            return s_wrapTransformsMemento.GetOrAdd(key, _ => FindMapToMessage(messageMapper)
                .GetOtherWrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step));
        }

        private IOrderedEnumerable<UnwrapWithAttribute> FindUnwrapTransforms<T>(IAmAMessageMapperAsync<T> messageMapper) where T : class, IRequest
        {
            var key = messageMapper.GetType();
            return s_unWrapTransformsMemento.GetOrAdd(key, _ => FindMapToRequest(messageMapper)
                .GetOtherUnwrapsInPipeline()
                .OrderByDescending(attribute => attribute.Step));
        }

        private MethodInfo FindMapToMessage<TRequest>(IAmAMessageMapperAsync<TRequest> messageMapper) where TRequest : class, IRequest
            => MapperMethodDiscovery.FindMapToMessageAsync(messageMapper.GetType(), typeof(TRequest))
               ?? throw new ConfigurationException($"No MapToMessageAsync method found on mapper '{messageMapper.GetType().Name}' for request type '{typeof(TRequest).Name}'");

        private MethodInfo FindMapToRequest<TRequest>(IAmAMessageMapperAsync<TRequest> messageMapper) where TRequest : class, IRequest
            => MapperMethodDiscovery.FindMapToRequestAsync(messageMapper.GetType())
               ?? throw new ConfigurationException($"No MapToRequestAsync method found on mapper '{messageMapper.GetType().Name}'");

        private TransformPipelineTracer TraceWrapPipeline<TRequest>(WrapPipelineAsync<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private TransformPipelineTracer TraceUnwrapPipeline<TRequest>(UnwrapPipelineAsync<TRequest> pipeline) where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "New wrap pipeline created for: {Message} of {Pipeline}")]
            public static partial void NewWrapPipelineCreated(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Debug, "Unwrap attributes on MapToMessage method for mapper of: {Message} in {Pipeline}, will be ignored")]
            public static partial void UnwrapAttributesOnMapToMessageMethodIgnored(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Debug, "New unwrap pipeline created for: {Message} of {Pipeline}")]
            public static partial void NewUnwrapPipelineCreated(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Debug, "Wrap attributes on MapToRequest method for mapper of: {Message} in {Pipeline}, will be ignored")]
            public static partial void WrapAttributesOnMapToRequestMethodIgnored(ILogger logger, string message, TransformPipelineTracer pipeline);

            [LoggerMessage(LogLevel.Warning, "No message transformer factory configured, so no transforms will be created but {TransformCount} configured")]
            public static partial void NoMessageTransformerFactoryConfigured(ILogger logger, int transformCount);

            [LoggerMessage(LogLevel.Warning, "Failed to release the probe mapper resolved by HasPipeline for {Request}; the answer is unaffected")]
            public static partial void FailedToReleaseProbeMapper(ILogger logger, Exception ex, string request);
        }
    }
}

