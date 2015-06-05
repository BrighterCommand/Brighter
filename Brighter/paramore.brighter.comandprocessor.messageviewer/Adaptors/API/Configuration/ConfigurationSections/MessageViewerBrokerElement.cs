using System.Configuration;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration.ConfigurationSections
{
    public class MessageViewerBrokerElement : ConfigurationElement
    {
        [ConfigurationProperty("typeName", IsKey = true, IsRequired = true)]
        public string TypeName
        {
            get { return (string)base["typeName"]; }
            set { base["typeName"] = value; }
        }
    }
}