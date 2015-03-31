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
