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
