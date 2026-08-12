// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using NATS.Client.JetStream.Models;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Publication configuration for publishing to a NATS JetStream stream, persisting messages for at-least-once delivery.
/// </summary>
/// <remarks>
/// The publication topic is used as the subject the stream subscribes to. Unless
/// <see cref="StreamConfiguration"/> provides an explicit stream, a stream named after the topic (with characters
/// invalid in a stream name replaced by '-') is created, validated, or assumed according to
/// <see cref="Publication.MakeChannels"/>.
/// </remarks>
public class NatsStreamPublication : NatsPublication
{
    /// <summary>
    /// Gets or sets the explicit stream configuration used when the stream is created.
    /// </summary>
    /// <value>The <see cref="StreamConfig"/>, or <see langword="null"/> to derive one from the publication topic.</value>
    public StreamConfig? StreamConfiguration { get; set; }
}

/// <summary>
/// Represents a JetStream publication for NATS, associating a specific message type with the publication.
/// </summary>
/// <typeparam name="TRequest">The type of request that this publication handles.</typeparam>
public class NatsStreamPublication<TRequest> : NatsStreamPublication
    where TRequest : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsStreamPublication{T}"/> class.
    /// </summary>
    public NatsStreamPublication()
    {
        RequestType = typeof(TRequest);
    }
}
