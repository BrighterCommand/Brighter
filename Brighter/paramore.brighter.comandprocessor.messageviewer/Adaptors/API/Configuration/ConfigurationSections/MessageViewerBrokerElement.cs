using System.Configuration;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration.ConfigurationSections
{
    public class MessageViewerBrokerElement : ConfigurationElement
    {
        [ConfigurationProperty("assemblyQualifiedName", IsKey = true, IsRequired = true)]
        public string AssemblyQualifiedName
        {
            get { return (string)base["assemblyQualifiedName"]; }
            set { base["assemblyQualifiedName"] = value; }
        }
    }
}