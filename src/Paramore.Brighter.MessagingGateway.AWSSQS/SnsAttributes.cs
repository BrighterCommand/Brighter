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

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class SnsAttributes
{
    /// <summary>
    /// The policy that defines how Amazon SNS retries failed deliveries to HTTP/S endpoints
    /// Ignored if TopicARN is set
    /// </summary>
    public string? DeliveryPolicy { get; set; } = null;

    /// <summary>
    /// The JSON serialization of the topic's access control policy.
    /// The policy that defines who can access your topic. By default, only the topic owner can publish or subscribe to the topic.
    /// Ignored if TopicARN is set
    /// </summary>
    public string? Policy { get; set; } = null;
        
    /// <summary>
    /// A list of resource tags to use when creating the publication
    /// Ignored if TopicARN is set
    /// </summary>
    public List<Tag> Tags => [];

    /// <summary>
    /// The <see cref="SnsSqsType"/>.
    /// </summary>
    public SnsSqsType Type { get; set; } = SnsSqsType.Standard;

    /// <summary>
    /// Enable content based deduplication for Fifo Topics
    /// </summary>
    public bool ContentBasedDeduplication { get; set; } = true;
}
