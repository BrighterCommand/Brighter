// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-26-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="Feed.cs" company="">
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using paramore.brighter.restms.core.Extensions;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    ///    Feed - a destination for messages published by applications.
    ///    Feeds follow these rules:
    ///    A feed is a write-only ordered stream of messages received from one or more writers.
    ///    The order of messages in a feed is stable per writer.
    ///    Feeds deliver messages into pipes, according to the joins defined on the feed.
    ///    Clients can create dynamic feeds for their own use.
    ///    To create a new feed the client POSTs a feed document to the parent domain URI.
    ///    The server MAY implement a set of configured public feeds.
    ///    http://www.restms.org/spec:2
    /// </summary>
    public class Feed : Resource, IAmAnAggregate
    {
        const string FEED_URI_FORMAT = "http://{0}/restms/feed/{1}";

        /// <summary>
        /// Initializes a new instance of the <see cref="Feed"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="feedType">Type of the feed.</param>
        /// <param name="title">The title.</param>
        /// <param name="license">The license.</param>
        public Feed(Name name, FeedType feedType = FeedType.Direct, Title title = null, Name license = null)
        {
            Type = feedType;
            Name = name;
            Title = title;
            License = license;
            Version = new AggregateVersion(0);
            Href = new Uri(string.Format(FEED_URI_FORMAT, Globals.HostName,Name.Value));
            Joins = new RoutingTable();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Feed"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="version">The version.</param>
        /// <param name="feedType">Type of the feed.</param>
        /// <param name="title">The title.</param>
        /// <param name="license">The license.</param>
        public Feed(Name name, AggregateVersion version, FeedType feedType = FeedType.Direct, Title title = null, Name license = null)
            : this(name, feedType, title, license)
        {
            Version = version;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>The type.</value>
        public FeedType Type { get; private set; }
        /// <summary>
        /// Gets the <see cref="Title"/>
        /// </summary>
        /// <value>The title.</value>
        public Title Title { get; private set; }
        /// <summary>
        /// Gets the license <see cref="Name"/>.
        /// </summary>
        /// <value>The license.</value>
        public Name License { get; private set; }

        /// <summary>
        /// Gets the <see cref="Identity"/>.
        /// </summary>
        /// <value>The identifier.</value>
        public Identity Id
        {
            get {return new Identity(Name.Value); }
        }
        /// <summary>
        /// Gets the <see cref="AggregateVersion"/>
        /// </summary>
        /// <value>The version.</value>
        public AggregateVersion Version { get; private set; }
        /// <summary>
        /// Gets the <see cref="RoutingTable"/> of joins.
        /// </summary>
        /// <value>The joins.</value>
        public RoutingTable Joins { get; private set; }

        /// <summary>
        /// Adds the join to the <see cref="Joins"/> collection.
        /// </summary>
        /// <param name="join">The join.</param>
        public void AddJoin(Join join)
        {
            Joins[join.Address] = new Join[] {join};
        }

        public IEnumerable<Pipe> AddMessage(Message message)
        {
            var updatedPipes = new List<Pipe>();
            var matchingJoins = Joins[message.Address];
            if (matchingJoins.Any())
            {
                matchingJoins.Each(join =>
                                   {
                                       var pipe = join.Pipe;
                                       pipe.AddMessage(new Message(message));
                                       updatedPipes.Add(pipe);
                                       
                                   });
                message.Content.Dispose();
            }

            return updatedPipes;
        }
    }
}
