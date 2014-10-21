// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="Join.cs" company="">
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

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    /// Join - a relationship between a pipe and a feed.
    /// Joins follow these rules:
    /// Joins specify criteria by which feeds route messages into pipes.
    /// Joins are always dynamic and always private.
    /// Clients MAY create joins at runtime, after creating pipes.
    /// To create a new join the client POSTs a join specification to the parent pipe URI.
    /// If either the feed or the pipe for a join is deleted, the join is also deleted.
    /// http://www.restms.org/spec:2
    /// </summary>
    public class Join
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="feedHref">The feed href.</param>
        public Join(Address address, Uri feedHref)
        {
            Address = address;
            FeedHref = feedHref;
            Type = JoinType.Default;
        }

        /// <summary>
        /// Gets the <see cref="Address"/>
        /// </summary>
        /// <value>The address.</value>
        public Address Address { get; private set; }
        /// <summary>
        /// Gets the feed href as a <see cref="Uri"/>
        /// </summary>
        /// <value>The feed href.</value>
        public Uri FeedHref { get; private set; }
        /// <summary>
        /// Gets the <see cref="JoinType"/>
        /// </summary>
        /// <value>The type.</value>
        public JoinType Type { get; private set; }
    }
}
