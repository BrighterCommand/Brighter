using System.Collections.Generic;
using System.Configuration;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguraton
{
    public class ConnectionElements: ConfigurationElementCollection
    {
        readonly List<ConnectionElement> elements = new List<ConnectionElement>();

        protected override ConfigurationElement CreateNewElement()
        {
            var newElement = new ConnectionElement();
            elements.Add(newElement);
            return newElement;
        }

        public ConfigurationElement this[int i]
        {
            get
            {
                return (ConfigurationElement )BaseGet(i);
            }
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ConnectionElement) element).ConnectionName;
        }
    }
}