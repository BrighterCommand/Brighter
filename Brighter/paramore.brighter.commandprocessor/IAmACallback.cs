// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 12-02-2015
//
// Last Modified By : ian
// Last Modified On : 12-02-2015
// ***********************************************************************
// <copyright file="IAmACallback.cs" company="Ian Cooper">
//     Copyright \u00A9  2014 Ian Cooper
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
    /// Intended for use with request-response messaging scenarios, where an asynchonous handler is expected to post a reply back over a
    /// private queue to a caller
    /// </summary>
    public interface IAmACallback
    {
        /// <summary>
        /// Who do we reply to
        /// </summary>
        /// <value>The routing key.</value>
        string RoutingKey { get; }

        /// <summary>
        /// How does the sender match our response to their request
        /// </summary>
        /// <value>The correlation identifier.</value>
        Guid CorrelationId { get; }
    }
}