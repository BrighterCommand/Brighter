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
using Common.Logging;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor
{
    internal class LifetimeScope : IAmALifetime
    {
        private readonly IAmAHandlerFactory handlerFactory;
        private readonly ILog logger;
        private readonly List<IHandleRequests> trackedObjects = new List<IHandleRequests>();

        public LifetimeScope(IAmAHandlerFactory handlerFactory, ILog logger = null)
        {
            this.handlerFactory = handlerFactory;
            this.logger = logger;
        }

        public int TrackedItemCount
        {
            get { return trackedObjects.Count; }
        }

        public void Add(IHandleRequests instance)
        {
            trackedObjects.Add(instance);
            if (logger != null)
                logger.Debug(m => m("Tracking instance {0} of type {1}", instance.GetHashCode(), instance.GetType()));
        }

        public void Dispose()
        {
            trackedObjects.Each((trackedItem) =>
            {
                //free disposableitems
                handlerFactory.Release(trackedItem);
                if (logger != null)
                    logger.Debug(
                        m =>
                            m("Releasing handler instance {0} of type {1}", trackedItem.GetHashCode(), trackedItem.GetType()));
            });

            //clear our tracking
            trackedObjects.Clear();
        }
    }
}
