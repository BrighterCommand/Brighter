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
    [DataContract(Name = "feed"), XmlRoot(ElementName = "feed")]
    public class RestMSFeed
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSFeed"/> class.
        /// </summary>
        public RestMSFeed() { /* required for serialization */}
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMSFeed"/> class.
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

        [DataMember(Name = "licence"), XmlAttribute(AttributeName = "licence")]
        public string Licence { get; set; }
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }
}