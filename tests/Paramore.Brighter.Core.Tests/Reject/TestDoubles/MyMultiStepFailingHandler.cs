#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Reject.Attributes;

namespace Paramore.Brighter.Core.Tests.Reject.TestDoubles
{
    /// <summary>
    /// A handler with RejectMessageOnError at step 0 (outermost) and RequestLogging at step 1 (inner).
    /// The handler throws an exception which should be caught by the outermost RejectMessageOnError handler.
    /// </summary>
    internal sealed class MyMultiStepFailingHandler : RequestHandler<MyCommand>
    {
        public const string EXCEPTION_MESSAGE = "Inner handler failure caught by outermost handler";

        public static bool HandlerCalled { get; set; }

        [RejectMessageOnError(step: 0)]  // Outermost - should catch all exceptions
        [RequestLogging(1, HandlerTiming.Before)]  // Inner handler
        public override MyCommand Handle(MyCommand command)
        {
            HandlerCalled = true;
            throw new InvalidOperationException(EXCEPTION_MESSAGE);
        }
    }
}
