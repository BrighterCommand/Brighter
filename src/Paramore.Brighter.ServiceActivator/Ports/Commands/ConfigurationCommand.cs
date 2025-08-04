#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.ServiceActivator.Ports.Commands
{
    /// <summary>
    /// Enum ConfigurationMessageType
    /// </summary>
    public enum ConfigurationCommandType
    {
        /// <summary>
        /// The c m_ stopall
        /// </summary>
        CM_STOPALL = 0,
        CM_STARTALL = 1,
        CM_STOPCHANNEL = 2,
        CM_STARTCHANNEL = 3
    }


    /// <summary>
    /// Class ConfigurationMessage.
    /// </summary>
    public class ConfigurationCommand : Command
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public ConfigurationCommandType Type { get; set; }

        /// <summary>
        /// Gets or sets the subscription name.
        /// </summary>
        public SubscriptionName SubscriptionName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCommand" /> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="subscriptionName"></param>
        public ConfigurationCommand(ConfigurationCommandType type, SubscriptionName subscriptionName) : base(Id.Random())
        {
            Type = type;
            SubscriptionName = subscriptionName;
        }
    }
}
