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
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class ValidateTopicByArn : IDisposable, IValidateTopic
    {
        private AmazonSimpleNotificationServiceClient _snsClient;

        public ValidateTopicByArn(AWSCredentials credentials, RegionEndpoint region)
        {
            _snsClient = new AmazonSimpleNotificationServiceClient(credentials, region);
        }

        public ValidateTopicByArn(AmazonSimpleNotificationServiceClient snsClient)
        {
            _snsClient = snsClient;
        }

        public virtual (bool, string TopicArn) Validate(string topicArn)
        {
            //List topics does not work across accounts - GetTopicAttributesRequest works within the region
            //List Topics is rate limited to 30 ListTopic transactions per second, and can be rate limited
            //So where we can, we validate a topic using GetTopicAttributesRequest

            bool exists = false;

            try
            {
                var topicAttributes = _snsClient.GetTopicAttributesAsync(
                    new GetTopicAttributesRequest(topicArn)
                ).GetAwaiter().GetResult();

                exists = ((topicAttributes.HttpStatusCode == HttpStatusCode.OK)  && (topicAttributes.Attributes["TopicArn"] == topicArn));
            }
            catch (InternalErrorException)
            {
                exists = false;
            }
            catch (NotFoundException)
            {
                exists = false;
            }
            catch (AuthorizationErrorException)
            {
                exists = false;
            }

            return (exists, topicArn);
        }

        public void Dispose()
        {
            _snsClient?.Dispose();
        }
    }
}
