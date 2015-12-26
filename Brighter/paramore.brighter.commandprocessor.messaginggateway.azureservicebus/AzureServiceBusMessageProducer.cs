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

using System;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.azureservicebus
{
    public class AzureServiceBusMessageProducer : IAmAMessageProducerSupportingDelay
    {
        private readonly ILog logger;
        private readonly AzureServiceBusMessagingGatewayConfigurationSection configuration;
        private readonly MessagingFactory factory;

        public AzureServiceBusMessageProducer(ILog logger)
        {
            this.logger = logger;
            this.configuration = AzureServiceBusMessagingGatewayConfigurationSection.GetConfiguration();
            var endpoint = ServiceBusEnvironment.CreateServiceUri("sb", this.configuration.Namespace.Name, String.Empty);
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(this.configuration.SharedAccessPolicy.Name, this.configuration.SharedAccessPolicy.Key);
            var settings = new MessagingFactorySettings
            {
                TransportType = TransportType.Amqp,
                OperationTimeout = TimeSpan.FromMinutes(5),
                TokenProvider = tokenProvider
            };
            this.factory = MessagingFactory.Create(endpoint, settings);
        }

        public void Send(Message message)
        {
            SendWithDelay(message);
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            logger.DebugFormat("AzureServiceBusMessageProducer: Publishing message to namespace {0}", this.configuration.Namespace);
            var messageSender = factory.CreateMessageSender(message.Header.Topic);
            messageSender.Send(new BrokeredMessage(message));
            logger.DebugFormat("AzureServiceBusMessageProducer: Published message to namespace {0}", this.configuration.Namespace);
        }

        public bool DelaySupported
        {
            get { return true; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AzureServiceBusMessageProducer() 
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!factory.IsClosed)
                {
                    factory.Close();
                }
            }
        }
    }
}
