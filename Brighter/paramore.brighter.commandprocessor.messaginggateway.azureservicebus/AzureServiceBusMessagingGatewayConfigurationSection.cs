#region Licence
/* The MIT License (MIT)
Copyright © 2015 Yiannis Triantafyllopoulos <yiannis.triantafyllopoulos@gmail.com>

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

using System.Configuration;

namespace paramore.brighter.commandprocessor.messaginggateway.azureservicebus
{
    class AzureServiceBusMessagingGatewayConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("namespace", IsRequired = true)]
        public Namespace Namespace
        {
            get { return this["namespace"] as Namespace; }
            set { this["namespace"] = value; }
        }

        [ConfigurationProperty("sharedAccessPolicy", IsRequired = true)]
        public SharedAccessPolicy SharedAccessPolicy
        {
            get { return this["sharedAccessPolicy"] as SharedAccessPolicy; }
            set { this["sharedAccessPolicy"] = value; }
        }

        public static AzureServiceBusMessagingGatewayConfigurationSection GetConfiguration()
        {
            var configuration =
                ConfigurationManager.GetSection("azureServiceBusMessagingGateway") as AzureServiceBusMessagingGatewayConfigurationSection;

            if (configuration != null) return configuration;

            return new AzureServiceBusMessagingGatewayConfigurationSection();
        }
    }

    class Namespace : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }
    }

    class SharedAccessPolicy : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("key", DefaultValue = "", IsRequired = true)]
        public string Key
        {
            get { return this["key"] as string; }
            set { this["key"] = value; }
        }
    }
}
