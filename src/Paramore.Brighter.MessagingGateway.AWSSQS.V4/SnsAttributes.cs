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

using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The attributes we use to confgure an SNS Topic
/// </summary>
public class SnsAttributes
{
    /// <summary>
    /// Creates a new instance of a type used to configure an SNS Topic
    /// </summary>
    /// <param name="deliveryPolicy">Determines how AWS retries failed deliveries to HTTP/S subscribers to the topic. Default is null.</param>
    /// <param name="policy">The JSON serialization of the topic's access control policy. Default is null.</param>
    /// <param name="type">The <see cref="SqsType"/>The <see cref="SqsType"/> which lets you set FIFO or Standard. Default is <see cref="SqsType.Standard"/></param>
    /// <param name="contentBasedDeduplication">For a FIFO queue, do we deduplicate messages based on content. Default is true</param>
    public SnsAttributes(string? deliveryPolicy = null, string? policy = null, SqsType type = SqsType.Standard, bool contentBasedDeduplication = true)
    {
        DeliveryPolicy = deliveryPolicy;
        Policy = policy;
        Type = type;
        ContentBasedDeduplication = contentBasedDeduplication;
    }

    /// <summary>
    /// The policy that defines how Amazon SNS retries failed deliveries to HTTP/S endpoints
    /// Ignored if TopicARN is set
    /// </summary>
    public string? DeliveryPolicy { get; }
    
    /// <summary>
    /// Creates a new instance of the <see cref="SnsAttributes"/> class. All attributes will be default values.
    /// </summary>
    public static SnsAttributes Empty { get; } = new();

    /// <summary>
    /// The JSON serialization of the topic's access control policy.
    /// The policy that defines who can access your topic. By default, only the topic owner can publish or subscribe to the topic.
    /// Ignored if TopicARN is set
    /// </summary>
    public string? Policy { get; }
        
    /// <summary>
    /// A list of resource tags to use when creating the publication  Ignored if TopicARN is set
    /// </summary>
    public List<Tag> Tags => [];

    /// <summary>
    /// The <see cref="SqsType"/>.
    /// </summary>
    public SqsType Type { get; set; }

    /// <summary>
    /// Enable content based deduplication for Fifo Topics
    /// </summary>
    public bool ContentBasedDeduplication { get; }
}
