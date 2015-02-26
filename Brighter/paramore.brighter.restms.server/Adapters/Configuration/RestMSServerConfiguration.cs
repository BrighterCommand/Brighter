// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Configuration;

namespace paramore.brighter.restms.server.Adapters.Configuration
{
    public class RestMSServerConfiguration : ConfigurationSection
    {
        [ConfigurationProperty("address")]
        public RestMSUriSpecification Address
        {
            get { return this["address"] as RestMSUriSpecification; }
            set { this["address"] = value; }
        }

        [ConfigurationProperty("admin")]
        public RestMSAdminSpecification Admin
        {
            get { return this["admin"] as RestMSAdminSpecification; }
            set { this["admin"] = value; }
        }

        public static RestMSServerConfiguration GetConfiguration()
        {
            var configuration =
                ConfigurationManager.GetSection("restMSServer") as RestMSServerConfiguration;

            if (configuration != null)
                return configuration;

            return new RestMSServerConfiguration();
        }
    }

    public class RestMSUriSpecification : ConfigurationElement
    {
        [ConfigurationProperty("uri", DefaultValue = "http://localhost:3416/", IsRequired = true)]
        public Uri Uri
        {
            get { return (Uri)this["uri"]; }
            set { this["uri"] = value; }
        }
    }
    public class RestMSAdminSpecification : ConfigurationElement
    {
        [ConfigurationProperty("id", DefaultValue = "dh37fgj492je", IsRequired = true)]
        public string Id
        {
            get { return this["id"] as string; }
            set { this["id"] = value; }
        }

        [ConfigurationProperty("user", DefaultValue = "Guest", IsRequired = true)]
        public string User
        {
            get { return this["user"] as string; }
            set { this["user"] = value; }
        }

        [ConfigurationProperty("key", DefaultValue = "wBgvhp1lZTr4Tb6K6+5OQa1bL9fxK7j8wBsepjqVNiQ=", IsRequired = true)]
        public string Key
        {
            get { return this["key"] as string; }
            set { this["key"] = value; }
        }
    }
}
