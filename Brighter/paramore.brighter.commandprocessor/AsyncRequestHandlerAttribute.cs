// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : Fred
// Created          : 2015-12-21
//                    Based on Pipelines.cs
//
// Last Modified By : Fred
// Last Modified On : 2015-12-21
// ***********************************************************************
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

using System;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class AsyncRequestHandlerAttribute.
    /// To satisfy orthogonal concerns it is possible to create a pipeline of awaitable <see cref="IHandleRequestsAsync"/> handlers. The 'target' handler should handle the domain
    /// logic, the other handlers in the pipeline should handle Quality of Service concerns or similar orthogonal concerns. We use an approach of attributing the <see cref="IHandleRequestsAsync{T}.HandleAsync"/>
    /// method to indicate the other handlers in the pipeline that handle orthogonal concerns. This approach is preferred over fluent-pipeline configuration
    /// because it allows you to easily see orthogonal concerns within the context of the target handler. In this sense Brighter is 'opinionated' about approach.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class AsyncRequestHandlerAttribute : Attribute
    {
        private readonly int _step;

        private readonly HandlerTiming _timing;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRequestHandlerAttribute"/> class.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        protected AsyncRequestHandlerAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        {
            _step = step;
            _timing = timing;
        }

        //We use this to pass params from the attribute into the instance of the handler
        //if you need to pass additional params to your handler, use this
        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public virtual object[] InitializerParams()
        {
            return new object[0];
        }

        //In which order should we run this, within the pre or post sequence for the main target?
        /// <summary>
        /// Gets the step.
        /// </summary>
        /// <value>The step.</value>
        public int Step
        {
            get { return _step; }
        }

        //Should we run this before or after the main target?
        /// <summary>
        /// Gets the timing.
        /// </summary>
        /// <value>The timing.</value>
        public HandlerTiming Timing
        {
            get { return _timing; }
        }

        //What type do we implement for the Filter in the Command Processor Pipeline
        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public abstract Type GetHandlerType();
    }
}