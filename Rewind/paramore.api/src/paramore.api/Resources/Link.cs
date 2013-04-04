using System.Runtime.Serialization;
using System.Xml.Serialization;
using Paramore.Adapters.Presentation.API.Translators;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    //See http://stackoverflow.com/questions/4858798/datacontract-xml-serialization-and-xml-attributes
    //for how to get attributes with DataContract
    //N.B. Slows performance

    [DataContract]
    internal class Link
    {
        private string relName;
        private string href1;

        public Link(string relName, string resourceName, string id)
        {
            this.rel = BuildRel(relName);
            this.href = BuildHttpLink(resourceName, id) ;
        }

        public Link(string relName, string href)
        {
            this.rel = BuildRel(relName);
            this.href = BuildHref(href);
        }


        [DataMember, XmlAttribute]
        internal string rel { get; set; }

        [DataMember, XmlAttribute]
        internal string href { get; set; }

        public override string ToString()
        {
            return string.Format("<link {0} {1}>", rel, href);
        }

        private string BuildRel(string relName)
        {
            return string.Format("rel='{0}'", relName);
        }

        private string BuildHttpLink(string resourceName, string id)
        {
            return string.Format("href='//{0}/{1}/{2}'", ParamoreGlobals.HostName, resourceName, id);
        }

        private string BuildHref(string href)
        {
            return string.Format("href='{0}'", href);
        }
    }
}