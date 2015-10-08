// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration.ConfigurationSections
{
    internal class MessageViewerSection : ConfigurationSection
    {
        private const string BrighterConfigSectionName = "brighter.messageViewer";

        public static MessageViewerSection GetViewerSection
        {
            get
            {
                var configSection = ConfigurationManager.GetSection(BrighterConfigSectionName) as MessageViewerSection;
                if (configSection == null)
                {
                    throw new ConfigurationErrorsException("Mis-configured brighter");
                }
                return configSection;
            }
        }

        [ConfigurationProperty("port", DefaultValue=3579, IsRequired=true)]
        public int Port
        {
            get
            {
                return this["port"] is int ? (int) this["port"] : -1;
            }
        }

        [ConfigurationProperty("stores", IsRequired = true, IsDefaultCollection = true)]
        public MessageViewerStoresElementCollection Stores
        {
            get { return (MessageViewerStoresElementCollection)this["stores"]; }
        }

        [ConfigurationProperty("producer", IsRequired = false, IsDefaultCollection = false)]
        public MessageViewerProducerElement Producer
        {
            get { return (MessageViewerProducerElement)this["producer"]; }
        }

        //<configuration>
        //    <configSections>
        //        <section name="brighter.messageViewer" type="paramore.brighter.comandprocessor.messageviewer.Hosting.MessageViewerSection, paramore.brighter.comandprocessor.messageviewer"/>
        //    </configSections>
        //<brighter.messageViewer port="3579">
        //  <stores>
        //    <clear />
        //    <add name="Contoso" type="http://www.contoso.com" connectionstring="0" />
        //  </stores>
        //  <broker
        //      assemblyName="paramore.brighter.commandprocessor.messaginggateway.rmq"
        //      factoryTypeName="paramore.brighter.commandprocessor.messaginggateway.rmq.RmqMessageProducerFactory">
        //  </broker>
        //+ would need
        // <configSections>
        //   <section name="rmqMessagingGateway" type="paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration.RMQMessagingGatewayConfigurationSection, paramore.brighter.commandprocessor.messaginggateway.rmq" allowLocation="true" allowDefinition="Everywhere" />
        // </configSections>
        // <rmqMessagingGateway>
        //   <amqpUri uri="amqp://guest:guest@localhost:5672/%2f" />
        //   <exchange name="paramore.brighter.exchange" />
        // </rmqMessagingGateway>
        //OR
        //  <broker
        //      assemblyName="paramore.brighter.commandprocessor.messaginggateway.awssqs"
        //      factoryTypeName="paramore.brighter.commandprocessor.messaginggateway.awssqs.SqsMessageProducerFactory">
        //  </broker>
        //+ would need
        // <configSections>
        //     <section name="aws" type="Amazon.AWSSection, AWSSDK" />
        // </configSections>
        // <aws profileName="brighter.sqs.test" region="eu-west-1" />
        //</brighter.messageViewer> 
        //</configuration>
    }
}