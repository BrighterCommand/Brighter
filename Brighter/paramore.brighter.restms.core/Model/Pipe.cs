// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="Pipe.cs" company="">
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
    ///    Pipe - a source of messages delivered to applications.
    ///    Pipes follow these rules:
    ///    A pipe is a read-only ordered stream of messages meant for a single reader.
    ///    The order of messages in a pipe is stable only for a single feed.
    ///    Pipes receive messages from joins, according to the joins defined between the pipe and the feed.
    ///    Clients MUST create pipes for their own use: all pipes are private and dynamic.
    ///    To create a new pipe the client POSTs a pipe document to the parent domain's URI.
    ///    The server MAY do garbage collection on unused, or overflowing pipes.
    ///    http://www.restms.org/spec:2
    /// </summary>
    public class Pipe : Resource, IAmAnAggregate
    {
        const string PIPE_URI_FORMAT = "http://{0}/restms/pipe/{1}";
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
        /// Gets the title.
        /// </summary>
        /// <value>The title.</value>
        public Title Title { get; private set; }
        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>The type.</value>
        public PipeType Type { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipe"/> class.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="pipeType">Type of the pipe.</param>
        /// <param name="title">The title.</param>
        public Pipe(Identity identity, string pipeType, Title title= null)
        {
            Id = identity;
            Title = title;
            Name = new Name(Id.Value);
            Type = (PipeType) Enum.Parse(typeof (PipeType), pipeType);
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, identity.Value));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipe"/> class.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="pipeType">Type of the pipe.</param>
        /// <param name="title">The title.</param>
        public Pipe(Identity identity, PipeType pipeType, Title title = null)
        {
            Id = identity;
            Title = title;
            Name = new Name(Id.Value);
            Type = pipeType;
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, identity.Value));
        }
    }
}
