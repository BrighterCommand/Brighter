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
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using paramore.brighter.commandprocessor.messaginggateway.azureservicebus.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.azureservicebus
{
    /// <summary>
    /// Provides access to Azure Service Bus, ensuring that the required topics exist.
    /// Uses the REST API for Azure Service Bus as the AMQP .NET Lite library only supports AMQP 1.0
    /// and thus has no understanding of notions like queues, subscriptions, and topics.
    /// </summary>
    public class MessageGateway : IDisposable
    {
        private static readonly ILog _logger = LogProvider.For<MessageGateway>();

        private readonly AzureServiceBusMessagingGatewayConfiguration _configuration;
        private readonly HttpClient _client = new HttpClient();

        public MessageGateway(AzureServiceBusMessagingGatewayConfiguration configuration)
        {
            _configuration = configuration;
            _client.BaseAddress = _configuration.Namespace.BaseAddress;
        }

        protected void EnsureTopicExists(string topicName)
        {
            if (!TopicExists(topicName))
            {
                PutTopic(topicName);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageGateway()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client.Dispose();
            }
        }

        private string GetSASToken(string SASKeyName, string SASKeyValue)
        {
            var fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var expiry = Convert.ToString((int)fromEpochStart.TotalSeconds + 3600);
            var stringToSign = WebUtility.UrlEncode(_client.BaseAddress.AbsoluteUri) + "\n" + expiry;
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SASKeyValue));

            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                WebUtility.UrlEncode(_client.BaseAddress.AbsoluteUri), WebUtility.UrlEncode(signature), expiry, SASKeyName);
            return sasToken;
        }


        private void PutTopic(string topicName)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                GetSASToken(_configuration.SharedAccessPolicy.Name, _configuration.SharedAccessPolicy.Key)
                );

            _logger.DebugFormat("\nCreating topic {0}", topicName);

            // Prepare the body of the create queue request 
            var requestBody = @"<entry xmlns=""http://www.w3.org/2005/Atom""> <title type=""text"">" + topicName + @"</title> <content type=""application/xml""> <TopicDescription xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.microsoft.com/netservices/2010/10/servicebus/connect"" /> </content> </entry>";

            var response = _client.PutAsync(topicName, new StringContent(requestBody)).Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new ConfigurationException(string.Format("Error creating topic on Azure Service BUS. Management API returned a {0) with {1}", response.StatusCode, response.Content.ReadAsStringAsync().Result));
            }
        }

        private bool TopicExists(string topicName)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                GetSASToken(_configuration.SharedAccessPolicy.Name, _configuration.SharedAccessPolicy.Key)
                );

            _logger.DebugFormat("\nCreating topic {0}", topicName);

            var response = _client.GetAsync(topicName).Result;

            return !response.IsSuccessStatusCode;
        }
    }
}