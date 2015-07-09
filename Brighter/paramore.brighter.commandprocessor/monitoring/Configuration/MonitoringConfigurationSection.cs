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

using System.Configuration;

namespace paramore.brighter.commandprocessor.monitoring.Configuration
{
    public class MonitoringConfigurationSection : ConfigurationSection
    {
        public static MonitoringConfigurationSection  GetConfiguration()
        {
            var configuration = ConfigurationManager.GetSection("monitoring") as MonitoringConfigurationSection ;

            if (configuration != null)
                return configuration;

            return new MonitoringConfigurationSection ();
        }

        [ConfigurationProperty("monitor")]
        public MonitorConfiguration Monitor
        {
            get { return this["monitor"] as MonitorConfiguration; }
            set { this["monitor"] = value; }
        }

    }


    public class MonitorConfiguration : ConfigurationElement
    {
        [ConfigurationProperty("isMonitoringEnabled", DefaultValue = "false", IsRequired = true)]
        public bool IsMonitoringEnabled
        {
            get { return (bool)this["isMonitoringEnabled"]; }
            set { this["isMonitoringEnabled"] = value; }
        }

        [ConfigurationProperty("instanceName", DefaultValue = "", IsRequired = true)]
        public string InstanceName
        {
            get { return (string) this["instanceName"]; }
            set { this["instanceName"] = value; }
        }
    }
}
