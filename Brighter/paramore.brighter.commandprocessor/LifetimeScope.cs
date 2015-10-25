// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="IRequestContext.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using System.Collections.Generic;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor
{
    internal class LifetimeScope : IAmALifetime
    {
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly ILog _logger;
        private readonly List<IHandleRequests> _trackedObjects = new List<IHandleRequests>();

        public LifetimeScope(IAmAHandlerFactory handlerFactory) 
            :this(handlerFactory, LogProvider.GetCurrentClassLogger())
        {}

        public LifetimeScope(IAmAHandlerFactory handlerFactory, ILog logger)
        {
            _handlerFactory = handlerFactory;
            _logger = logger;
        }

        public int TrackedItemCount
        {
            get { return _trackedObjects.Count; }
        }

        public void Add(IHandleRequests instance)
        {
            _trackedObjects.Add(instance);
            if (_logger != null)
                _logger.DebugFormat("Tracking instance {0} of type {1}", instance.GetHashCode(), instance.GetType());
        }

        public void Dispose()
        {
            _trackedObjects.Each((trackedItem) =>
            {
                //free disposable items
                _handlerFactory.Release(trackedItem);
                if (_logger != null)
                    _logger.DebugFormat("Releasing handler instance {0} of type {1}", trackedItem.GetHashCode(), trackedItem.GetType());
            });

            //clear our tracking
            _trackedObjects.Clear();
        }
    }
}
