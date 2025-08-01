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
using Paramore.Brighter.Policies.Handlers;

namespace Paramore.Brighter.Policies.Attributes
{
    /// <summary>
    /// Class TimeoutPolicyAttribute.
    /// This attribute supports setting a timeout on a handler. It is intended for legacy scenarios where the network calls or resource pools used by a handler
    /// do not natively support a timeout and can be used to prevent a handler from blocking on one of these operations. You should not use it where native
    /// timeouts are available, use the native timeout instead.
    /// </summary>
    [Obsolete("It is recommended to use UsePolicyAttribute or UseResiliencePipelineAttribute instead")]
    public class TimeoutPolicyAttribute : RequestHandlerAttribute
    {
        private readonly int _milliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutPolicyAttribute"/> class.
        /// </summary>
        /// <param name="milliseconds">The milliseconds.</param>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        public TimeoutPolicyAttribute(int milliseconds, int step, HandlerTiming timing = HandlerTiming.Before) : base(step, timing)
        {
            _milliseconds = milliseconds;
        }

        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public override object[] InitializerParams()
        {
            return new object[] { _milliseconds };
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof(TimeoutPolicyHandler<>);
        }
    }
}
