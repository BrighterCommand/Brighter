// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="Message.cs" company="">
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
using paramore.brighter.restms.core.Extensions;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    /// A message specification consists of a set of properties (attributes of the message element), a set of header elements, a set of content references, and a set of embedded contents. These work as follows:
    /// The address is used in routing, and has feed-specific semantics.
    /// The headers may used in routing, and to carry arbitrary information.
    /// The message_id is an application-assigned identifier string.
    /// The reply_to tells an eventual recipient where to send a reply message.
    /// The content hrefs refer to previously staged contents.
    /// The content elements, if they have an element value, hold embedded contents.
    /// Implementations may add other properties with implementation-defined meaning.
    /// http://www.restms.org/spec:2

    /// </summary>
    public class Message: Resource, IAmAnAggregate
    {
        const string MESSAGE_URI_FORMAT = "http://{0}/restms/message/{1}";

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="headers"></param>
        /// <param name="attachment"></param>
        /// <param name="replyTo"></param>
        public Message(Address address, NameValueCollection headers, Attachment attachment, Uri replyTo = null)
        {
            Address = address;
            ReplyTo = replyTo;
            MessageId = Guid.NewGuid();
            Headers = new MessageHeaders();
            var keys = headers.AllKeys;
            keys.Each(key => Headers.AddHeader(key, headers[key]));
            Content = new MessageContent(attachment.ContentType, attachment.TransferEncoding, attachment.ContentStream);
            Href = new Uri(string.Format(MESSAGE_URI_FORMAT, Globals.HostName, MessageId));
            Id = new Identity(MessageId.ToString());
            Version = new AggregateVersion(0);
        }

        public Message(string address, NameValueCollection headers, Attachment attachment, string replyTo = null)
            : this(new Address(address), headers, attachment)
        {
            if (replyTo != null)
            {
                ReplyTo = new Uri(replyTo);
            }
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
        /// 
        public AggregateVersion Version { get; private set; }

        /// <summary>
        /// Gets the address.
        /// </summary>
        /// <value>The address.</value>
        /// 
        public Address Address { get; private set; }

        /// <summary>
        /// Gets the message identifier.
        /// </summary>
        /// <value>The message identifier.</value>
        public Guid MessageId { get; private set; }

        /// <summary>
        /// Gets the reply to.
        /// </summary>
        /// <value>The reply to.</value>
        public Uri ReplyTo { get; private set; }

        /// <summary>
        /// Gets the feed href.
        /// </summary>
        /// <value>The feed href.</value>
        public Uri FeedHref { get; private set; }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        /// <value>The headers.</value>
        public MessageHeaders Headers { get; private set; }

        /// <summary>
        /// Gets the content.
        /// </summary>
        /// <value>The content.</value>
        public MessageContent Content { get; private set; }

    }
}
