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
using paramore.brighter.commandprocessor.policy.Handlers;

namespace paramore.brighter.commandprocessor.policy.Attributes
{
    /// <summary>
    /// Class FallbackPolicyAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class FallbackPolicyAsyncAttribute : RequestHandlerAttribute
    {
        private readonly bool _backstop;
        private readonly bool _circuitBreaker;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAttribute" /> class.
        /// </summary>
        /// <param name="backstop">if set to <c>true</c> [backstop].</param>
        /// <param name="circuitBreaker">if set to <c>true</c> [circuit breaker].</param>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        public FallbackPolicyAsyncAttribute(
            bool backstop, 
            bool circuitBreaker, 
            int step, 
            HandlerTiming timing = HandlerTiming.Before) 
            : base(step, timing)
        {
            _backstop = backstop;
            _circuitBreaker = circuitBreaker;
        }

        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public override object[] InitializerParams()
        {
           return new object[] {_backstop, _circuitBreaker};
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof (FallbackPolicyHandlerRequestHandlerAsync<>);
        }

    }}
