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

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguraton
{
    public class ServiceActivatorConfigurationSection : ConfigurationSection
    {
        public static ServiceActivatorConfigurationSection GetConfiguration()
        {
            var configuration = ConfigurationManager.GetSection("serviceActivatorConnections")as ServiceActivatorConfigurationSection;

            if (configuration != null)
                return configuration;

            return new ServiceActivatorConfigurationSection();
        }

        [ConfigurationProperty("connections", Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        [ConfigurationCollection(typeof(ConnectionElement))]
        public ConnectionElements Connections
        {
            get
            {
                return (ConnectionElements ) this["connections"] ;
            }
        }
    }

    public class ConnectionElement : ConfigurationElement
    {   
        [ConfigurationProperty("connectionName", DefaultValue = "", IsRequired = true)]
        public string ConnectionName 
        {
            get { return this["connectionName"] as string ; }
            set { this["connectionName"] = value; }
        }

        [ConfigurationProperty("channelName", DefaultValue = "", IsRequired = true)]
        public string ChannelName 
        {
            get { return this["channelName"] as string ; }
            set { this["channelName"] = value; }
        }

        [ConfigurationProperty("routingKey", DefaultValue = "", IsRequired = true)]
        public string RoutingKey
        {
            get { return this["routingKey"] as string; }
            set { this["routingKey"] = value; }
        }

        [ConfigurationProperty("dataType", DefaultValue = "", IsRequired = true)]
        public string DataType
        {
            get { return this["dataType"] as string ; }
            set { this["dataType"] = value; }
        }

        [ConfigurationProperty("noOfPerformers", DefaultValue = "1", IsRequired = false)]
        public int NoOfPerformers
        {
            get { return Convert.ToInt32(this["noOfPerformers"]); }
            set { this["noOfPerformers"] = value; }
        }

        [ConfigurationProperty("timeOutInMilliseconds", DefaultValue = "200", IsRequired = true)]
        public int TimeoutInMiliseconds 
        {
            get { return Convert.ToInt32(this["timeOutInMilliseconds"]); }
            set { this["timeOutInMilliseconds"] = value; }
        }
        [ConfigurationProperty("requeueCount", DefaultValue = "-1", IsRequired = false)]
        public int RequeueCount
        {
            get { return Convert.ToInt32(this["requeueCount"]); }
            set { this["requeueCount"] = value; }
        }
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
