// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
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

namespace Paramore.Brighter
{
    /// <summary>
    /// Enum HandlerTiming
    /// The Subscriber Registry registers 'target' handlers for commands or events.
    /// A <see cref="RequestHandlerAttribute"/> identifies handlers to run in the pipeline with the target handler, to deal with 'orthogonal' concerns
    /// Those handlers can run either before or after the target handler, and the timing value indicates where they should run
    /// Note that handlers explicitly call the next handler in sequence, so 'child' handlers always run in the scope of their 'parent' handlers, which means
    /// that you can choose to only execute code in a 'parent' only after a 'child' handler has executed. So you can control order of operation by that approach
    /// and do not need to use an After handler for that.
    /// </summary>
    public enum HandlerTiming
    {
        /// <summary>
        /// Execute this 'orthogonal' handler before the 'target' handler
        /// </summary>
        Before = 0,
        /// <summary>
        /// Execute this 'orthogonal' handler after the 'target' handler 
        /// </summary>
        After = 1
    }
}
