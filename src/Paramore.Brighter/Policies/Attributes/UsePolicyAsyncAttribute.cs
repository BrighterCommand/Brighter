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
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;

namespace Paramore.Brighter.Policies.Attributes
{
    /// <summary>
    /// Class UsePolicyAttribute.
    /// This attribute supports the use of <a href="https://github.com/App-vNext/Polly">Polly</a> to provide quality of service around exceptions
    /// thrown from subsequent steps in the handler pipeline. A Polly Policy can be used to support a Retry or Circuit Breaker approach to exception handling
    /// Policies used by the attribute are identified by a string based key, which is used as a lookup into an <see cref="PolicyRegistry" /> and it is
    /// assumed that you have registered required policies with a Policy Registry such as <see cref="PolicyRegistry" /> and configured that as a
    /// dependency of the <see cref="CommandProcessor" /> using the <see cref="CommandProcessorBuilder" />
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [Obsolete("Migrate to UseResiliencePipelineAsync")]
    public class UsePolicyAsyncAttribute : RequestHandlerAttribute
    {
        private readonly List<string> _policies = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="UsePolicyAsyncAttribute" /> class.
        /// </summary>
        /// <param name="policy">The policy key, used as a lookup into an <see cref="PolicyRegistry" />.</param>
        /// <param name="step">The step.</param>
        public UsePolicyAsyncAttribute(string policy, int step) : base(step, HandlerTiming.Before)
        {
            _policies.Add(policy);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UsePolicyAsyncAttribute" /> class.
        /// </summary>
        /// <param name="policies">A set of policy keys, used as a lookup into an <see cref="PolicyRegistry" />.</param>
        /// <param name="step">The step</param>
        public UsePolicyAsyncAttribute(string[] policies, int step) : base(step, HandlerTiming.Before)
        {
            policies.Each(p => _policies.Add(p));
        }

        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public override object[] InitializerParams()
        {
            return new object[] { _policies};
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof(ExceptionPolicyHandlerAsync<>);
        }
    }
}
