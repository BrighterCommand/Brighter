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
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    /// <summary>
    /// Tracks the handler instances a handler pipeline has created, and carries the pipeline's own
    /// <see cref="IAmAScope"/> handle. The default implementation of <see cref="IAmALifetime"/>.
    /// </summary>
    public sealed partial class HandlerLifetimeScope : IAmALifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<HandlerLifetimeScope>();

        private readonly IAmAHandlerFactorySync? _handlerFactorySync;
        private readonly List<IHandleRequests> _trackedObjects = new List<IHandleRequests>();
        private readonly List<IHandleRequestsAsync> _trackedAsyncObjects = new List<IHandleRequestsAsync>();
        private readonly IAmAHandlerFactoryAsync? _asyncHandlerFactory;
        private readonly IAmAScope? _pipelineScope;

        public HandlerLifetimeScope(IAmAHandlerFactorySync handlerFactorySync, IAmAScope? pipelineScope = null)
            : this(handlerFactorySync, null, pipelineScope)
        {}

        public HandlerLifetimeScope(IAmAHandlerFactoryAsync asyncHandlerFactory, IAmAScope? pipelineScope = null)
            : this(null, asyncHandlerFactory, pipelineScope)
        {}

        public HandlerLifetimeScope(
            IAmAHandlerFactorySync? handlerFactorySync,
            IAmAHandlerFactoryAsync? asyncHandlerFactory,
            IAmAScope? pipelineScope = null)
        {
            _handlerFactorySync = handlerFactorySync;
            _asyncHandlerFactory = asyncHandlerFactory;
            _pipelineScope = pipelineScope;
        }

        public int TrackedItemCount => _trackedObjects.Count + _trackedAsyncObjects.Count;

        public IAmAScope? PipelineScope => _pipelineScope;

        public void Add(IHandleRequests instance)
        {
            if (_handlerFactorySync == null)
                throw new ArgumentException("An instance of a handler can not be added without a HandlerFactory.");
            _trackedObjects.Add(instance);
            Log.TrackingInstance(s_logger, instance.GetHashCode(), instance.GetType());
        }

        public void Add(IHandleRequestsAsync instance)
        {
            if (_asyncHandlerFactory == null)
                throw new ArgumentException("An instance of an async handler can not be added without an AsyncHandlerFactory.");
            _trackedAsyncObjects.Add(instance);
            Log.TrackingAsyncHandlerInstance(s_logger, instance.GetHashCode(), instance.GetType());
        }

        public void Dispose()
        {
            //release every tracked handler, sync then async, catching per item so one failing
            //Release does not skip the rest or leave the tracking lists uncleared
            foreach (var trackedItem in _trackedObjects)
            {
                try
                {
                    _handlerFactorySync?.Release(trackedItem, this);
                    Log.ReleasingHandlerInstance(s_logger, trackedItem.GetHashCode(), trackedItem.GetType());
                }
                catch (Exception exception)
                {
                    Log.FailedToReleaseHandler(s_logger, trackedItem.GetHashCode(), trackedItem.GetType(), trackedItem.Name, exception);
                }
            }

            foreach (var trackedItem in _trackedAsyncObjects)
            {
                try
                {
                    _asyncHandlerFactory?.Release(trackedItem, this);
                    Log.ReleasingAsyncHandlerInstance(s_logger, trackedItem.GetHashCode(), trackedItem.GetType());
                }
                catch (Exception exception)
                {
                    Log.FailedToReleaseHandler(s_logger, trackedItem.GetHashCode(), trackedItem.GetType(), trackedItem.Name, exception);
                }
            }

            //clear our tracking so this scope does not outlive its disposal holding references
            _trackedObjects.Clear();
            _trackedAsyncObjects.Clear();

            //dispose the pipeline scope handle last and unconditionally, holding any failure
            try
            {
                _pipelineScope?.Dispose();
            }
            catch (Exception exception)
            {
                Log.FailedToDisposePipelineScope(s_logger, exception);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "Tracking instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void TrackingInstance(ILogger logger, int instanceHashCode, Type handlerType);

            [LoggerMessage(LogLevel.Debug, "Tracking async handler instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void TrackingAsyncHandlerInstance(ILogger logger, int instanceHashCode, Type handlerType);

            [LoggerMessage(LogLevel.Debug, "Releasing handler instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void ReleasingHandlerInstance(ILogger logger, int instanceHashCode, Type handlerType);

            [LoggerMessage(LogLevel.Debug, "Releasing async handler instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void ReleasingAsyncHandlerInstance(ILogger logger, int instanceHashCode, Type handlerType);

            [LoggerMessage(LogLevel.Error, "Failed to release handler instance {InstanceHashCode} of type {HandlerType} ({HandlerName})")]
            public static partial void FailedToReleaseHandler(ILogger logger, int instanceHashCode, Type handlerType, HandlerName handlerName, Exception exception);

            [LoggerMessage(LogLevel.Error, "Failed to dispose the handler pipeline's own scope; the pipeline's result is unaffected")]
            public static partial void FailedToDisposePipelineScope(ILogger logger, Exception exception);
        }
    }
}

