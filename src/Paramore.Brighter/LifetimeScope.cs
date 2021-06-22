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
    internal class LifetimeScope : IAmALifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<LifetimeScope>();

        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly List<IHandleRequests> _trackedObjects = new List<IHandleRequests>();
        private readonly List<IHandleRequestsAsync> _trackedAsyncObjects = new List<IHandleRequestsAsync>();
        private readonly IAmAHandlerFactoryAsync _asyncHandlerFactory;
        private Guid _scopeId;
        private const string _scopeIdentifier = "scopeIdentifier";

        public LifetimeScope(IAmAHandlerFactory handlerFactory) 
            : this(handlerFactory, null)
        {}

        public LifetimeScope(IAmAHandlerFactoryAsync asyncHandlerFactory) 
            : this(null, asyncHandlerFactory)
        {}

        public LifetimeScope(IAmAHandlerFactory handlerFactory, IAmAHandlerFactoryAsync asyncHandlerFactory) 
        {
            _handlerFactory = handlerFactory;
            _asyncHandlerFactory = asyncHandlerFactory;
            handlerFactory?.TryCreateScope(this);
        }

        public int TrackedItemCount => _trackedObjects.Count + _trackedAsyncObjects.Count;

        public void Add(IHandleRequests instance)
        {
            if (_handlerFactory == null)
                throw new ArgumentException("An instance of a handler can not be added without a HandlerFactory.");
            _trackedObjects.Add(instance);
            SetScopeId(instance);
            s_logger.LogDebug("Tracking instance {InstanceHashCode} of type {HandlerType}", instance.GetHashCode(), instance.GetType());
        }

        public void Add(IHandleRequestsAsync instance)
        {
            if (_asyncHandlerFactory == null)
                throw new ArgumentException("An instance of an async handler can not be added without an AsyncHandlerFactory.");
            _trackedAsyncObjects.Add(instance);
            SetScopeId(instance);
            s_logger.LogDebug("Tracking async handler instance {InstanceHashCode} of type {HandlerType}", instance.GetHashCode(), instance.GetType());
        }

        private void SetScopeId(IHandleRequests instance) 
        {
            //err if id not set
            instance.Context.Bag.Add(_scopeIdentifier, _scopeId);
        }

        private void SetScopeId(IHandleRequestsAsync instance) 
        {
            //err if id not set
            instance.Context.Bag.Add(_scopeIdentifier, _scopeId);
        }
        
        public void SetScopeId(Guid scopeId)
        {
            _scopeId = scopeId;
        }

        public virtual void Dispose()
        {
            bool? tryReleaseScope = _handlerFactory?.TryReleaseScope(_trackedObjects);
            if (tryReleaseScope.HasValue && tryReleaseScope.Value)
            {
                //duplicated here to early exit as non-regular flow
                _trackedObjects.Clear();
                _trackedAsyncObjects.Clear();
                return;
            }
            
            _trackedObjects.Each((trackedItem) =>
            {
                //free disposable items
                _handlerFactory.Release(trackedItem);
                s_logger.LogDebug("Releasing handler instance {InstanceHashCode} of type {HandlerType}", trackedItem.GetHashCode(), trackedItem.GetType());
            });

            _trackedAsyncObjects.Each(trackedItem =>
            {
                //free disposable items
                _asyncHandlerFactory.Release(trackedItem);
                s_logger.LogDebug("Releasing async handler instance {InstanceHashCode} of type {HandlerType}", trackedItem.GetHashCode(), trackedItem.GetType());
            });

            //clear our tracking
            _trackedObjects.Clear();
            _trackedAsyncObjects.Clear();
        }
    }
}
