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

namespace Paramore.Brighter.FeatureSwitch.Providers
{
    public class FluentConfigRegistry : IAmAFeatureSwitchRegistry
    {
        public MissingConfigStrategy MissingConfigStrategy { get; set; } = MissingConfigStrategy.Exception;

        private readonly IDictionary<Type, FeatureSwitchStatus> _switches;

        public FluentConfigRegistry()
        {
            _switches = new Dictionary<Type, FeatureSwitchStatus>();
        }

        public FluentConfigRegistry StatusOf<T>(FeatureSwitchStatus status)
        {
            _switches.Add(typeof(T), status);

            return this;
        }

        public FeatureSwitchStatus StatusOf(Type handler)
        {
            var configExists = _switches.ContainsKey(handler);

            if (!configExists)
            {
                if (MissingConfigStrategy is MissingConfigStrategy.SilentOn)
                {
                    return FeatureSwitchStatus.On;
                }

                if (MissingConfigStrategy is MissingConfigStrategy.SilentOff)
                {
                    return FeatureSwitchStatus.Off;
                }

                if (MissingConfigStrategy is MissingConfigStrategy.Exception)
                {
                    throw new ConfigurationException($"Handler of type {handler.Name} does not have a Feature Switch configuration!");
                }
            }

            return _switches[handler];
        }
    }
}
