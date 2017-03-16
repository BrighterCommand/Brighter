// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 01-26-2015
//
// Last Modified By : ian
// Last Modified On : 02-26-2015
// ***********************************************************************
// <copyright file="HeaderResult.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class HeaderResult.
    /// </summary>
    /// <typeparam name="TResult">The type of the t result.</typeparam>
    public class HeaderResult<TResult>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderResult{TResult}"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="success">if set to <c>true</c> [success].</param>
        public HeaderResult(TResult result, bool success)
        {
            Success = success;
            Result = result;
        }

        /// <summary>
        /// Maps the specified map.
        /// </summary>
        /// <typeparam name="TNew">The type of the t new.</typeparam>
        /// <param name="map">The map.</param>
        /// <returns>HeaderResult&lt;TNew&gt;.</returns>
        public HeaderResult<TNew> Map<TNew>(Func<TResult, HeaderResult<TNew>> map)
        {
            if (Success)
                return map(Result);
            return HeaderResult<TNew>.Empty();
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="HeaderResult{TResult}"/> is success.
        /// </summary>
        /// <value><c>true</c> if success; otherwise, <c>false</c>.</value>
        public bool Success { get; private set; }
        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <value>The result.</value>
        public TResult Result { get; private set; }

        /// <summary>
        /// Empties this instance.
        /// </summary>
        /// <returns>HeaderResult&lt;TResult&gt;.</returns>
        public static HeaderResult<TResult> Empty()
        {
            return new HeaderResult<TResult>(default(TResult), false);
        }
    }
}