// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-07-2014
//
// Last Modified By : ian
// Last Modified On : 10-09-2014
// ***********************************************************************
// <copyright file="AddFeedToDomainCommand.cs" company="">
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
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    /// <summary>
    /// Class AddFeedToDomainCommand.
    /// </summary>
    public class AddFeedToDomainCommand : Command
    {
        /// <summary>
        /// Gets the name of the domain.
        /// </summary>
        /// <value>The name of the domain.</value>
        public string DomainName { get; private set; }
        /// <summary>
        /// Gets the name of the feed.
        /// </summary>
        /// <value>The name of the feed.</value>
        public string FeedName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddFeedToDomainCommand"/> class.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="feedName">Name of the feed.</param>
        public AddFeedToDomainCommand(string domainName, string feedName)
            : base(Guid.NewGuid())
        {
            DomainName = domainName;
            FeedName = feedName;
        }
    }
}