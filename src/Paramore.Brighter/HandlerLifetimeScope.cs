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
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    internal class HandlerLifetimeScope : IAmALifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<HandlerLifetimeScope>();

        private readonly IAmAHandlerFactorySync? _handlerFactorySync;
        private readonly List<IHandleRequests> _trackedObjects = new List<IHandleRequests>();
        private readonly List<IHandleRequestsAsync> _trackedAsyncObjects = new List<IHandleRequestsAsync>();
        private readonly IAmAHandlerFactoryAsync? _asyncHandlerFactory;

        public HandlerLifetimeScope(IAmAHandlerFactorySync handlerFactorySync) 
            : this(handlerFactorySync, null)
        {}

        public HandlerLifetimeScope(IAmAHandlerFactoryAsync asyncHandlerFactory) 
            : this(null, asyncHandlerFactory)
        {}

        public HandlerLifetimeScope(IAmAHandlerFactorySync? handlerFactorySync, IAmAHandlerFactoryAsync? asyncHandlerFactory) 
        {
            _handlerFactorySync = handlerFactorySync;
            _asyncHandlerFactory = asyncHandlerFactory;
        }

        public int TrackedItemCount => _trackedObjects.Count + _trackedAsyncObjects.Count;

        public void Add(IHandleRequests instance)
        {
            if (_handlerFactorySync == null)
                throw new ArgumentException("An instance of a handler can not be added without a HandlerFactory.");
            _trackedObjects.Add(instance);
            s_logger.LogDebug("Tracking instance {InstanceHashCode} of type {HandlerType}", instance.GetHashCode(), instance.GetType());
        }

        public void Add(IHandleRequestsAsync instance)
        {
            if (_asyncHandlerFactory == null)
                throw new ArgumentException("An instance of an async handler can not be added without an AsyncHandlerFactory.");
            _trackedAsyncObjects.Add(instance);
            s_logger.LogDebug("Tracking async handler instance {InstanceHashCode} of type {HandlerType}", instance.GetHashCode(), instance.GetType());
        }

        public void Dispose()
        {
            _trackedObjects.Each((trackedItem) =>
            {
                //free disposable items
                _handlerFactorySync?.Release(trackedItem, this);
                s_logger.LogDebug("Releasing handler instance {InstanceHashCode} of type {HandlerType}", trackedItem.GetHashCode(), trackedItem.GetType());
            });

            _trackedAsyncObjects.Each(trackedItem =>
            {
                //free disposable items
                _asyncHandlerFactory?.Release(trackedItem, this);
                s_logger.LogDebug("Releasing async handler instance {InstanceHashCode} of type {HandlerType}", trackedItem.GetHashCode(), trackedItem.GetType());
            });

            //clear our tracking
            _trackedObjects.Clear();
            _trackedAsyncObjects.Clear();
        }
    }
}
