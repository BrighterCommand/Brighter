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

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Creates a publication for an SNS Topic
/// </summary>
/// <remarks>
///  You should provide either the Arn, for a topic created in advance, or the attributes to create a topic
/// or find it from a name. 
/// </remarks>
public class SnsPublication : Publication
{
    /// <summary>
    /// Creates a publication for an SNS Topic
    /// </summary>
    /// <remarks>
    ///  You should provide either the Arn, for a topic created in advance, or the attributes to create a topic
    /// or find it from a name. 
    /// </remarks>
    /// <param name="findTopicBy">How should we look for the topic? Default is by <see cref="TopicFindBy.Convention"/>,
    /// which builds an Arn from name an account. <see cref="TopicFindBy.Name"/> will look up by name. If you already know the
    /// Arn, you can use <see cref="TopicFindBy.Arn"/>. If you use  <see cref="TopicFindBy.Arn"/> you MUST supply the "topicArn" parameter</param>
    /// <param name="topicArn">The Arn for the topic. You must set the "findTopicBy" to <see cref="TopicFindBy.Arn"/> in this case</param>
    /// <param name="topicAttributes">Optional: The attributes that describe the topic</param>
    public SnsPublication(TopicFindBy findTopicBy = TopicFindBy.Convention, string? topicArn = null, SnsAttributes? topicAttributes = null)
    {
        FindTopicBy = findTopicBy;
        TopicAttributes = topicAttributes;
        TopicArn = topicArn;
    }

    /// <summary>
    /// The routing key type.
    /// </summary>
    public ChannelType ChannelType { get; } = ChannelType.PubSub;

    /// <summary>
    /// Indicates how we should treat the routing key. If you want to use an Arn you should set <see cref="SnsPublication.TopicArn"/>.
    /// <see cref="TopicFindBy.Arn"/>: the routing key is an Arn
    /// <see cref="TopicFindBy.Convention"/>: the routing key is a name, so use convention to turn it an Arn under this account
    /// <see cref="TopicFindBy.Name"/>: treat the routing key as a name; use ListTopics to find it (rate limited 30/s)
    /// </summary>
    public TopicFindBy FindTopicBy { get; set; }        
    
    /// <summary>
    /// The attributes of the topic. If TopicARNs is set we will always assume that we do not
    /// need to create or validate the SNS Topic
    /// </summary>
    public SnsAttributes? TopicAttributes { get; set; }

    /// <summary>
    /// The topic arn as provided by AWS
    /// </summary>
    /// <remarks>
    /// If we want to supply the topic Arn and not use the <see cref="Publication.Topic"/> which is  a <see cref="RoutingKey"/>
    /// (which we will treat as a name and lookup), then you need to supply  the Arn. 
    /// </remarks>
   public string? TopicArn { get; set; } 
}

/// <summary>
/// Creates a publication for an SNS Topic
/// </summary>
/// <remarks>
///  You should provide either the Arn, for a topic created in advance, or the attributes to create a topic
/// or find it from a name. 
/// </remarks>
public class SnsPublication<T> : SnsPublication 
    where T: class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnsPublication{T}"/> class.
    /// </summary>
    public SnsPublication()
    {
        RequestType = typeof(T);
    }
}

