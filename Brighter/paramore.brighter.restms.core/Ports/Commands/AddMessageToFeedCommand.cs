// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-16-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="AddMessageToFeedCommand.cs" company="">
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
using System.Collections.Specialized;
using System.Net.Mail;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class AddMessageToFeedCommand : Command
    {
        public string FeedName { get; private set; }
        public int MatchingJoins { get; set; }
        public string Address { get; private set; }
        public string ReplyTo { get; private set; }
        public NameValueCollection Headers { get; private set; }
        public Attachment Attachment { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="feedName"></param>
        /// <param name="address"></param>
        /// <param name="replyTo"></param>
        /// <param name="headers"></param>
        /// <param name="attachments"></param>
        /// <param name="name"></param>
        /// <param name="id">The identifier.</param>
        public AddMessageToFeedCommand(string feedName, string address, string replyTo, NameValueCollection headers, Attachment attachment) : base(Guid.NewGuid())
        {
            FeedName = feedName;
            Address = address;
            ReplyTo = replyTo;
            Headers = headers;
            Attachment = attachment;
        }
    }
}
