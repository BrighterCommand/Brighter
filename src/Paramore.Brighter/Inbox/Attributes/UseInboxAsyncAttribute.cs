﻿#region Licence
/* The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Inbox.Attributes
{
    /// <summary>
    /// Class UseAsyncCommandSourcingAttribute.
    /// We use this attribute to indicate that we want to use Event Sourcing, with async support, where the application state is the system of record
    /// but we want to store the commands that led to the current application state, so that we can rebuild application state
    /// or recreate commands
    /// See  <a href="http://martinfowler.com/eaaDev/EventSourcing.html">Martin Fowler Event Sourcing</a> for more on this approach.
    /// </summary>
    public class UseInboxAsyncAttribute : RequestHandlerAttribute
    {
        public bool OnceOnly { get; }
        public string? ContextKey { get; }
        public OnceOnlyAction OnceOnlyAction { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UseInboxAsyncAttribute"/> class.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="onceOnly">Should we prevent duplicate messages i.e. seen already</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timing">The timing.</param>
        /// <param name="onceOnlyAction">Action to take if prevent duplicate messages, and we receive a duplicate message</param>
        public UseInboxAsyncAttribute(int step, bool onceOnly = false, string? contextKey = null, HandlerTiming timing = HandlerTiming.Before, OnceOnlyAction onceOnlyAction = OnceOnlyAction.Throw)
            : base(step, timing)
        {
            OnceOnly = onceOnly;
            ContextKey = contextKey;
            OnceOnlyAction = onceOnlyAction;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UseInboxAsyncAttribute"/> class.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="onceOnly">Should we prevent duplicate messages i.e. seen already</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="timing">The timing.</param>
        /// <param name="onceOnlyAction">Action to take if prevent duplicate messages, and we receive a duplicate message</param>
        public UseInboxAsyncAttribute(int step, Type contextKey, bool onceOnly = false, HandlerTiming timing = HandlerTiming.Before, OnceOnlyAction onceOnlyAction = OnceOnlyAction.Throw)
            : this(step, onceOnly, contextKey.FullName, timing, onceOnlyAction)
        {
        }

        public override object?[] InitializerParams()
        {
            
            return new object?[]{OnceOnly, ContextKey, OnceOnlyAction};
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof (UseInboxHandlerAsync<>);
        }
    }
}
