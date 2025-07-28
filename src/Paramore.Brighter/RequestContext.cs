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
using System.Collections.Generic;
using System.Diagnostics;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class RequestContextFactory
    /// Any pipeline has a request context that allows you to flow information between instances of <see cref="IHandleRequests"/>
    /// The default in-memory <see cref="RequestContext"/> created by an <see cref="InMemoryRequestContextFactory"/> is suitable for most purposes
    /// and this interface is mainly provided for testing
    /// </summary>
    public class RequestContext : IRequestContext
    {
        private readonly ConcurrentDictionary<int, Activity> _spans = new();

        public RequestContext() { }
        
        private RequestContext(ConcurrentDictionary<string, object> bag)
        {
            Bag = new ConcurrentDictionary<string, object>(bag);
        }

        /// <inheritdoc />
        public RoutingKey? Topic { get; set; }

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
        /// Create a new instance of the Request Context
        /// </summary>
        /// <returns>New Instance of the message</returns>
        public IRequestContext CreateCopy()
            => new RequestContext(Bag)
            {
                Span = Span,
                Policies = Policies,
                FeatureSwitches = FeatureSwitches,
                OriginatingMessage = OriginatingMessage,
            };

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
        /// Gets the policies.
        /// </summary>
        /// <value>The policies.</value>
        public IPolicyRegistry<string>? Policies { get; set; }

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
    }
}
