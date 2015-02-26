// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
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
using System.Collections.Specialized;
using System.Net.Mail;
using paramore.brighter.restms.core.Extensions;

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
    public class Message : Resource
    {
        private Name _pipeName;
        private const string MESSAGE_URI_FORMAT = "http://{0}/restms/pipe/{1}/message/{2}";

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="headers"></param>
        /// <param name="attachment"></param>
        /// <param name="replyTo"></param>
        public Message(Address address, Uri feedHref, NameValueCollection headers, Attachment attachment, Uri replyTo = null)
        {
            Address = address;
            ReplyTo = replyTo;
            MessageId = Guid.NewGuid();
            Headers = new MessageHeaders();
            var keys = headers.AllKeys;
            keys.Each(key => Headers.AddHeader(key, headers[key]));
            Content = new MessageContent(attachment.ContentType, attachment.TransferEncoding, attachment.ContentStream);
            FeedHref = feedHref;
            Name = new Name(MessageId.ToString());
            ReplyTo = replyTo;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="feedHref">The feed href.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="attachment">The attachment.</param>
        /// <param name="replyTo">The reply to.</param>
        public Message(string address, string feedHref, NameValueCollection headers, Attachment attachment, string replyTo = null)
            : this(new Address(address), new Uri(feedHref), headers, attachment)
        {
            if (replyTo != null)
            {
                ReplyTo = new Uri(replyTo);
            }
        }

        /// <summary>
        /// Copy constructor for the <see cref="Message"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public Message(Message message)
        {
            Address = new Address(message.Address.Value);
            ReplyTo = message.ReplyTo != null ? new Uri(message.ReplyTo.AbsoluteUri) : null;
            MessageId = new Guid(message.MessageId.ToString());
            Headers = message.Headers.Copy();
            FeedHref = new Uri(message.FeedHref.AbsoluteUri);
            Name = new Name(message.Name.Value);
            Content = message.Content.Copy();
        }

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

        public Name PipeName
        {
            get { return _pipeName; }
            set
            {
                _pipeName = value;
                Href = new Uri(string.Format(MESSAGE_URI_FORMAT, Globals.HostName, _pipeName.Value, MessageId));
            }
        }
    }
}
