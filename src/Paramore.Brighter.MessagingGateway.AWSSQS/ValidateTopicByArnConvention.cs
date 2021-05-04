using System;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class ValidateTopicByArnConvention : ValidateTopicByArn, IValidateTopic
    {
        private readonly RegionEndpoint _region;
        private AmazonSecurityTokenServiceClient _stsClient;

        public ValidateTopicByArnConvention(AWSCredentials credentials, RegionEndpoint region) : base(credentials, region)
        {
            _region = region;

            _stsClient = new AmazonSecurityTokenServiceClient(credentials, region);
        }

        public override (bool, string TopicArn) Validate(string topic)
        {
            var topicArn = GetArnFromTopic(topic);
            return base.Validate(topicArn);
        }

        private string GetArnFromTopic(string topicName)
        {
            var callerIdentityResponse = _stsClient.GetCallerIdentityAsync(
                    new GetCallerIdentityRequest()
                )
                .GetAwaiter().GetResult();

            if (callerIdentityResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidCastException("Could not find identity of AWS account"); 

            return new Arn
            {
                Partition = _region.PartitionName,
                Service = "sns",
                Region = _region.SystemName,
                AccountId = callerIdentityResponse.Account,
                Resource = topicName
            }.ToString();
        }
    }
}
