// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Configuration;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguration
{
    public class ConnectionElement : ConfigurationElement
    {
        [ConfigurationProperty("connectionName", DefaultValue = "", IsRequired = true)]
        public string ConnectionName
        {
            get { return this["connectionName"] as string; }
            set { this["connectionName"] = value; }
        }

        [ConfigurationProperty("channelName", DefaultValue = "", IsRequired = true)]
        public string ChannelName
        {
            get { return this["channelName"] as string; }
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
            get { return this["dataType"] as string; }
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
}