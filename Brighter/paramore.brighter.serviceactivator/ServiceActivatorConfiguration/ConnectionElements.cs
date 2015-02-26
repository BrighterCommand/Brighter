// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Configuration;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguration
{
    public class ConnectionElements : ConfigurationElementCollection
    {
        private readonly List<ConnectionElement> _elements = new List<ConnectionElement>();

        protected override ConfigurationElement CreateNewElement()
        {
            var newElement = new ConnectionElement();
            _elements.Add(newElement);
            return newElement;
        }

        public ConfigurationElement this[int i]
        {
            get
            {
                return (ConfigurationElement)BaseGet(i);
            }
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ConnectionElement)element).ConnectionName;
        }
    }
}