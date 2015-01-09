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
using System.Configuration;

namespace paramore.brighter.commandprocessor.messaginggateway.restms.MessagingGatewayConfiguration
{
    public class RestMSMessagingGatewayConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("restMS")]
        public RestMSSpecification RestMS
        {
            get { return this["restMS"] as RestMSSpecification; }
            set { this["restMS"] = value; }
        }

        [ConfigurationProperty("feed", IsRequired = true)]
        public Feed Feed
        {
            get { return this["feed"] as Feed; }
            set { this["feed"] = value; }
        }

        public static RestMSMessagingGatewayConfigurationSection GetConfiguration()
        {
            var configuration =
                ConfigurationManager.GetSection("restMSMessagingGateway") as RestMSMessagingGatewayConfigurationSection;

            if (configuration != null)
                return configuration;

            return new RestMSMessagingGatewayConfigurationSection();
        }
    }

    public class RestMSSpecification : ConfigurationElement
    {
        [ConfigurationProperty("uri", DefaultValue = "http://localhost:3416/restms/domain/default", IsRequired = true)]
        public Uri Uri
        {
            get { return (Uri) this["uri"]; }
            set { this["uri"] = value; }
        }

        [ConfigurationProperty("id", DefaultValue = "Default", IsRequired = true)]
        public string Id
        {
            get { return this["id"] as string; }
            set { this["id"] = value; }
        }

        [ConfigurationProperty("user", DefaultValue = "Default", IsRequired = true)]
        public string User
        {
            get { return this["user"] as string; }
            set { this["user"] = value; }
        }

        [ConfigurationProperty("key", DefaultValue = "Default", IsRequired = true)]
        public string Key
        {
            get { return this["key"] as string; }
            set { this["key"] = value; }
        }
    }

    public class Feed : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "Default", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("type", DefaultValue = "Default")]
        public string Type
        {
            get { return this["type"] as string; }
            set { this["type"] = value; }
        }
    }
}