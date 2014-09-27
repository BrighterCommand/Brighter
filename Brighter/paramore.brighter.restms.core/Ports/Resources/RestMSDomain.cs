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
    [DataContract, XmlRoot]
    public class RestMSDomain
    {

        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
        [DataMember(Name = "profile"), XmlAttribute(AttributeName = "profile")]
        public RestMSProfile Profile {get; set;}
        [DataMember(Name = "feed"), XmlElement(ElementName = "feed")]
        public RestMSFeed[] Feeds;

        public RestMSDomain() {/*required for serialization*/}

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

    [DataContract, XmlRoot]
    public class RestMSProfile
    {

        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [DataMember(Name="hrer"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }

        public RestMSProfile() {/*required for serialization*/}

        public RestMSProfile(Profile profile)
        {
            Name = profile.Name.Value;
            Href = profile.Href.AbsoluteUri;
        }
    }

    public class RestMSFeed
    {
        public RestMSFeed() { /* required for serialization */}
        public RestMSFeed(Feed feed)
        {
            Type = feed.Type.ToString();
            Name = feed.Name.Value;
            Title = feed.Title.Value;
            Href = feed.Href.AbsoluteUri;
        }

        [DataMember(Name = "type"), XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }
        [DataMember(Name = "name"), XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [DataMember(Name = "title"), XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
        [DataMember(Name = "href"), XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }
}
