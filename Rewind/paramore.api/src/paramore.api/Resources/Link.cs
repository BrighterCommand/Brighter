using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Resources
{
    [XmlType("link")]
    public class Link
    {
        public Link(string relName, string resourceName, string id)
        {
            this.Rel = relName;
            this.HRef = string.Format("http://{0}/{1}/{2}", ParamoreGlobals.HostName, resourceName, id);
        }

        public Link(string relName, string href)
        {
            this.Rel = relName;
            this.HRef = href;
        }

        public Link()
        {
            //Required for serialiazation
        }

        [XmlAttribute("rel")]
        public string Rel { get; set; }
        [XmlAttribute("href")]
        public string HRef { get; set; }

        public override string ToString()
        {
            return string.Format("<link rel=\"{0}\" href=\"{1}\" />", Rel, HRef);
        }
    }
}