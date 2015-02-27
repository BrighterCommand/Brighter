// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-26-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
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

using System;
using System.Collections.Generic;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    ///    Domain - a set of resources managed by one RestMS server or virtual host.
    ///    Domains follow these rules:
    ///    A domain is a public collection of profiles, feeds, and pipes.
    ///    The server MAY implement multiple domains and MAY allow routing of messages between domains.
    ///    Domains MAY be used to partition resources for authenticated access control.
    ///    Domains are configured resources: clients do not create or destroy domains.
    ///    Domains act as namespaces for pipes and feeds.
    ///    The client and server must agree in advance on the domains that exist.
    ///    The server SHOULD implement a default public domain named "default".
    ///     http://www.restms.org/spec:2
    /// </summary>
    public class Domain : Resource, IAmAnAggregate
    {
        private readonly HashSet<Identity> _feeds = new HashSet<Identity>();
        private readonly HashSet<Identity> _pipes = new HashSet<Identity>();
        private const string DOMAIN_URI_FORMAT = "http://{0}/restms/domain/{1}";

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>The title.</value>
        public Title Title { get; private set; }
        /// <summary>
        /// Gets the profile.
        /// </summary>
        /// <value>The profile.</value>
        public Profile Profile { get; private set; }

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Identity Id
        {
            get { return new Identity(Name.Value); }
        }
        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public AggregateVersion Version { get; private set; }

        /// <summary>
        /// Gets the feeds.
        /// </summary>
        /// <value>The feeds.</value>
        public IEnumerable<Identity> Feeds
        {
            get { return _feeds; }
        }

        /// <summary>
        /// Gets the pipes.
        /// </summary>
        /// <value>The pipes.</value>
        public IEnumerable<Identity> Pipes
        {
            get { return _pipes; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Domain"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="title">The title.</param>
        /// <param name="profile">The profile.</param>
        public Domain(Name name, Title title, Profile profile)
        {
            Name = name;
            Title = title;
            Profile = profile;
            Version = new AggregateVersion(0);
            Href = new Uri(string.Format(DOMAIN_URI_FORMAT, Globals.HostName, Name.Value));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Domain"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="title">The title.</param>
        /// <param name="profile">The profile.</param>
        /// <param name="version">The version.</param>
        public Domain(Name name, Title title, Profile profile, AggregateVersion version)
            : this(name, title, profile)
        {
            Version = version;
        }
        /// <summary>
        /// Adds the feed.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public void AddFeed(Identity id)
        {
            _feeds.Add(id);
        }

        #region Equality operators
        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected bool Equals(Domain other)
        {
            return Equals(Name, other.Name);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Domain)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(Domain left, Domain right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(Domain left, Domain right)
        {
            return !Equals(left, right);
        }
        #endregion

        /// <summary>
        /// Removes the feed.
        /// </summary>
        /// <param name="identity">The identity.</param>
        public void RemoveFeed(Identity identity)
        {
            _feeds.RemoveWhere(feed => feed == identity);
        }

        /// <summary>
        /// Adds the pipe.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public void AddPipe(Identity id)
        {
            _pipes.Add(id);
        }
    }
}
