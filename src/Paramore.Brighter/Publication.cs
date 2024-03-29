﻿#region Licence
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

namespace Paramore.Brighter
{
    /// <summary>
    /// Contains configuration that is generic to producers - similar to Subscription for consumers
    /// Unlike <see cref="Subscription"/>, as it is passed to a constructor it is by convention over enforced at compile time
    /// Platform specific configuration goes into a <see cref="IAmGatewayConfiguration"/> derived class
    /// </summary>
    public class Publication
    {
        /// <summary>
        /// What do we do with infrastructure dependencies for the producer?
        /// </summary>
        public OnMissingChannel MakeChannels { get; set; }
        
        /// <summary>
        /// The topic this publication is for
        /// </summary>
        public RoutingKey Topic { get; set; }
        
        /// <summary>
        /// The type of the request that we expect to publish on this channel
        /// </summary>
        public Type RequestType { get; set; }
         
    }
}
