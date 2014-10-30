// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-27-2014
//
// Last Modified By : ian
// Last Modified On : 10-13-2014
// ***********************************************************************
// <copyright file="RestMS.cs" company="">
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

using System.Runtime.Serialization;
using System.Xml.Serialization;
using paramore.brighter.restms.core.Model;

namespace paramore.brighter.restms.core.Ports.Resources
{
    /// <summary>
    /// </summary>
    [DataContract(Name = "domain"), XmlRoot(ElementName = "domain")]
    public class RestMSDomain
    {

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
        /// <summary>
        /// Gets or sets the profile.
        /// </summary>
        /// <value>The profile.</value>
        [DataMember(Name = "profile"), XmlAttribute(AttributeName = "profile")]
        public RestMSProfile Profile {get; set;}
        /// <summary>
        /// The feeds
        /// </summary>
        [DataMember(Name = "feed"), XmlElement(ElementName = "feed")]
        public RestMSFeed[] Feeds;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSDomain" /> class.
        /// </summary>
        public RestMSDomain() {/*required for serialization*/}

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSDomain" /> class.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <param name="feeds">The feeds.</param>
        public RestMSDomain(Domain domain, Feed[] feeds)
        {
            Name = domain.Name.Value;
            Title = domain.Title.Value;
            Profile = new RestMSProfile(domain.Profile);
            Feeds = new RestMSFeed[feeds.Length];
            for (int i = 0; i < feeds.Length; i++)
            {
                Feeds[i] = new RestMSFeed(feeds[i]);;
            }
        }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "profile"), XmlRoot(ElementName = "profile")]
    public class RestMSProfile
    {

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name="hrer"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSProfile" /> class.
        /// </summary>
        public RestMSProfile() {/*required for serialization*/}

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSProfile" /> class.
        /// </summary>
        /// <param name="profile">The profile.</param>
        public RestMSProfile(Profile profile)
        {
            Name = profile.Name.Value;
            Href = profile.Href.AbsoluteUri;
        }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "feed"), XmlRoot(ElementName = "feed")]
    public class RestMSFeed
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSFeed" /> class.
        /// </summary>
        public RestMSFeed() { /* required for serialization */}
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSFeed" /> class.
        /// </summary>
        /// <param name="feed">The feed.</param>
        public RestMSFeed(Feed feed)
        {
            Type = feed.Type.ToString();
            Name = feed.Name.Value;
            Title = feed.Title.Value;
            Href = feed.Href.AbsoluteUri;
        }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the licence.
        /// </summary>
        /// <value>The licence.</value>
        [DataMember(Name = "licence"), XmlAttribute(AttributeName = "licence")]
        public string Licence { get; set; }
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "pipe"), XmlRoot(ElementName = "pipe")]
    public class RestMSPipeNew
    {

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "pipe"), XmlRoot(ElementName = "pipe")]
    public class RestMSPipeLink
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "pipe"), XmlRoot(ElementName = "pipe")]
    public class RestMSPipe
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }

        /// <summary>
        /// Gets or sets the joins.
        /// </summary>
        /// <value>The joins.</value>
        [DataMember(Name = "join"), XmlElement(ElementName = "join")]
        public RestMSJoin[] Joins { get; set; }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "join"), XmlRoot(ElementName = "join")]
    public class RestMSJoin
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }


    /// <summary>
    /// </summary>
    [DataContract(Name = "message"), XmlRoot(ElementName = "message")]
    public class RestMSMessageLink
    {
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
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
    }


    /// <summary>
    /// </summary>
    [DataContract(Name = "message"), XmlRoot(ElementName = "message")]
    public class RestMSMessage
    {
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
        public RestMSFeed Feed { get; set; }

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


    /// <summary>
    /// </summary>
    [DataContract(Name = "header"), XmlRoot(ElementName = "header")]
    public class RestMSMessageHeader
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [DataMember(Name = "value"), XmlAttribute(AttributeName = "value")]
        public string Value { get; set; }
    }

    /// <summary>
    /// </summary>
    [DataContract(Name = "content"), XmlRoot(ElementName = "content")]
    public class RestMSMessageContent
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }
        /// <summary>
        /// Gets or sets the encoding.
        /// </summary>
        /// <value>The encoding.</value>
        [DataMember(Name = "encoding"), XmlAttribute(AttributeName = "type")]
        public string Encoding { get; set; }
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [DataMember, XmlText]
        public string Value { get; set; } 
    }


}
