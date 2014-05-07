using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguraton
{
    public class ServiceActivatorConfigurationSection
    {
        public static ServiceActivatorConfigurationSection GetConfiguration()
        {
            var configuration = ConfigurationManager.GetSection("serviceActivatorConnections")as ServiceActivatorConfigurationSection;

            if (configuration != null)
                return configuration;

            return new ServiceActivatorConfigurationSection();
        }

        private ServiceActivatorConfigurationSection(){}

        [ConfigurationProperty("connections", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(ConnectionElement), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public ConnectionElements Connections { get; set; }

    }

    internal class ConnectionElement : ConfigurationElement
    {   
        [ConfigurationProperty("channelName", DefaultValue = "", IsRequired = true)]
        public string ChannelName { get; private set; }
        [ConfigurationProperty("dataType", DefaultValue = "", IsRequired = true)]
        public string DataType { get; private set; }
        [ConfigurationProperty("noOfPerformers", DefaultValue = "1", IsRequired = true)]
        public int NoOfPeformers { get; private set; }
        [ConfigurationProperty("timeOutInMilliseconds", DefaultValue = "200", IsRequired = true)]
        public int TimeoutInMiliseconds { get; private set; }
    }

    public class ConnectionElements: ConfigurationElementCollection
    {
        readonly List<ConnectionElement> elements = new List<ConnectionElement>();

        protected override ConfigurationElement CreateNewElement()
        {
            var newElement = new ConnectionElement();
            elements.Add(newElement);
            return newElement;
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return elements.FirstOrDefault(el => el == element);
        }


    }
}
