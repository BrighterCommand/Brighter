// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-05-2014
// ***********************************************************************
// <copyright file="RestMSMessage.cs" company="">
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

using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using paramore.brighter.restms.core.Model;

namespace paramore.brighter.restms.core.Ports.Resources
{
    /// <summary>
    /// Class RestMSMessage.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    [DataContract(Name = "message", Namespace = "")]
    public class RestMSMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSMessage"/> class.
        /// </summary>
        public RestMSMessage(){}

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSMessage"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public RestMSMessage(Message message)
        {
            Address = message.Address.Value;
            MessageId = message.MessageId.ToString();
            ReplyTo = message.ReplyTo == null ? "" : message.ReplyTo.AbsoluteUri;
            Feed = message.FeedHref.AbsoluteUri;
            Headers = message.Headers.All.Select(header => new RestMSMessageHeader(header)).ToArray();
            Content = new RestMSMessageContent(message.Content);
        }

        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        /// <value>The address.</value>
        [DataMember(Name = "address"), XmlAttribute(AttributeName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the message identifier.
        /// </summary>
        /// <value>The message identifier.</value>
        [DataMember(Name = "message_id"), XmlAttribute(AttributeName = "message_id")]
        public string MessageId { get; set; }

        /// <summary>
        /// Gets or sets the reply to.
        /// </summary>
        /// <value>The reply to.</value>
        [DataMember(Name = "reply_to"), XmlAttribute(AttributeName = "reply_to")]
        public string ReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the feed.
        /// </summary>
        /// <value>The feed.</value>
        [DataMember(Name = "feed"), XmlAttribute(AttributeName = "feed")]
        public string Feed { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        /// <value>The headers.</value>
        [DataMember(Name = "header"), XmlAttribute(AttributeName = "header")]
        public RestMSMessageHeader[] Headers { get; set; }

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        /// <value>The content.</value>
        [DataMember(Name = "content"), XmlAttribute(AttributeName = "content")]
        public RestMSMessageContent Content { get; set; }

    }
}