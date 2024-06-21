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
using System.Diagnostics;
using Paramore.Brighter.FeatureSwitch;
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
        private Message _originatingMessage;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestContext"/> class.
        /// </summary>
        public RequestContext()
        {
            Bag = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Gets the Span [Activity] associated with the request
        /// </summary>
        public Activity Span { get; set; }

        /// <summary>
        /// Gets the bag.
        /// </summary>
        /// <value>The bag.</value>
        public Dictionary<string, object> Bag { get; private set; }
        
        /// <summary>
        /// When we pass a requestContext through a receiver pipeline, we may want to pass the original message that started the pipeline.
        /// This is primarily useful for debugging - how did we get to this request?. But it is also useful for some request metadata that we
        /// do not want to transfer to the Request.
        ///</summary>
        /// <value>The originating message</value> 
        public Message OriginatingMessage
        {
            get { return _originatingMessage; }
            set
            {
                if (_originatingMessage == null) 
                    _originatingMessage = value;
                else 
                    throw new InvalidOperationException("You may only set the originating message once on the same context");
            }
                
            
        }
        
        /// <summary>
        /// Gets the policies.
        /// </summary>
        /// <value>The policies.</value>
        public IPolicyRegistry<string>  Policies { get; set; }
        
        /// <summary>
        /// Gets the Feature Switches
        /// </summary>
        public IAmAFeatureSwitchRegistry FeatureSwitches { get; set; }
    }
}
