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

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SnsPublication : Publication
    {
        /// <summary>
        /// Indicates how we should treat the routing key
        /// TopicFindBy.Arn -> the routing key is an Arn
        /// TopicFindBy.Convention -> The routing key is a name, but use convention to make an Arn for this account
        /// TopicFindBy.Name -> Treat the routing key as a name & use ListTopics to find it (rate limited 30/s)
        /// </summary>

        public TopicFindBy FindTopicBy { get; set; } = TopicFindBy.Convention;
        
        /// <summary>
        /// The attributes of the topic. If TopicARNs is set we will always assume that we do not
        /// need to create or validate the SNS Topic
        /// </summary>
        public SnsAttributes? SnsAttributes { get; set; }

        /// <summary>
        /// If we want to use topic Arns and not topics you need to supply  the Arn to use for any message that you send to us,
        /// as we use the topic from the header to dispatch to  an Arn.
        /// </summary>
        public string? TopicArn { get; set; }
    }
}
