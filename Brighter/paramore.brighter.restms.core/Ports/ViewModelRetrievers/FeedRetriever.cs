// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-08-2014
//
// Last Modified By : ian
// Last Modified On : 10-09-2014
// ***********************************************************************
// <copyright file="FeedRetriever.cs" company="">
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

using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    /// <summary>
    /// </summary>
    public class FeedRetriever
    {
        readonly IAmARepository<Feed> feedRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedRetriever"/> class.
        /// </summary>
        /// <param name="feedRepository">The feed repository.</param>
        public FeedRetriever(IAmARepository<Feed> feedRepository)
        {
            this.feedRepository = feedRepository;
        }

        /// <summary>
        /// Retrieves the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>RestMSFeed.</returns>
        /// <exception cref="paramore.brighter.restms.core.Ports.Common.FeedDoesNotExistException"></exception>
        public RestMSFeed Retrieve(Name name)
        {
            var feed = feedRepository[new Identity(name.Value)];

            if (feed == null)
            {
                throw new FeedDoesNotExistException();
            }

            return new RestMSFeed(feed);
        }
    }
}
