#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal class ValidateTopicByName : IValidateTopic
    {
        private readonly AmazonSimpleNotificationServiceClient _snsClient;

        public ValidateTopicByName(AWSCredentials credentials, RegionEndpoint region, Action<ClientConfig> clientConfigAction = null)
        {
            var clientFactory = new AWSClientFactory(credentials, region, clientConfigAction);
            _snsClient = clientFactory.CreateSnsClient();
        }
        
        public ValidateTopicByName(AmazonSimpleNotificationServiceClient snsClient)
        {
            _snsClient = snsClient;
        }
        
        //Note that we assume here that topic names are globally unique, if not provide the topic ARN directly in the SNSAttributes of the subscription
        //This approach can have be rate throttled at scale. AWS limits to 30 ListTopics calls per second, so it you have a lot of clients starting
        //you may run into issues
        public (bool, string TopicArn) Validate(string topicName)
        {
            var topic = _snsClient.FindTopicAsync(topicName).GetAwaiter().GetResult();
            return (topic != null, topic?.TopicArn);
        }
    }
}
