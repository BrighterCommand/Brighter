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
using paramore.brighter.restms.core.Ports.Common;

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
    public class Join : Resource, IAmAnAggregate
    {
        const string JOIN_URI_FORMAT = "http://{0}/restms/join/{1}";
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="pipe">The Pipe this joins the feed to</param>
        /// <param name="feedHref">The feed href for identifying the feed</param>
        /// <param name="address">The address pattern to match this join against.</param>
        public Join(Pipe pipe, Uri feedHref, Address address)
        {
            Address = address;
            FeedHref = feedHref;
            Pipe = pipe;
            Type = JoinType.Default;
            Name = new Name(Guid.NewGuid().ToString());
            Href = new Uri(string.Format(JOIN_URI_FORMAT, Globals.HostName, Name.Value));
            Id = new Identity(Name.Value);
            Version = new AggregateVersion(0);

        }

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Identity Id { get; private set; }

        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public AggregateVersion Version { get; private set; }

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
        /// The pipe we are attached to.
        /// </summary>
        /// <value>The pipe.</value>
        public Pipe Pipe { get; set; }

        /// <summary>
        /// Gets the <see cref="JoinType"/>
        /// </summary>
        /// <value>The type.</value>
        public JoinType Type { get; private set; }

        protected bool Equals(Join other)
        {
            return Name == other.Name;
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Join) obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static bool operator ==(Join left, Join right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Join left, Join right)
        {
            return !Equals(left, right);
        }
    }
}
