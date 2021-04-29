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
