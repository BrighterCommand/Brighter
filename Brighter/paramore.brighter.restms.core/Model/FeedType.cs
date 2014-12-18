// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-26-2014
//
// Last Modified By : ian
// Last Modified On : 09-26-2014
// ***********************************************************************
// <copyright file="FeedType.cs" company="">
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

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    /// </summary>
    public enum FeedType
    {
        /// <summary>
        /// A direct feed is point-to-point distribution to pipes with an identical address on a join
        /// </summary>
        Direct = 0,
        /// <summary>
        /// A fanout feed is publish-subscribe distribution to a set of joined pipes, addresses are not used
        /// </summary>
        Fanout,
        /// <summary>
        /// A topic feed is point-to-point distribution to pipes by matching all of or part of a routing key on a join
        /// </summary>
        Topic,
        /// <summary>
        /// A headers feed is routes message to pipes by matching the headers on the message to the expected headers on the join
        /// </summary>
        Headers,
        /// <summary>
        /// Like a rotator, but the feed is deleted when the last join is deleted
        /// </summary>
        Service,
        /// <summary>
        /// Round-robin distribution to pipes, using one join attached to the feed at a time
        /// </summary>
        Rotator,
        /// <summary>
        /// Implementation specific feed type
        /// </summary>
        System,
        /// <summary>
        /// The default feed type is equivalent to Direct
        /// </summary>
        Default
    }
}
