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

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Creates Brighter message producers for a set of NATS publications.
/// </summary>
/// <remarks>
/// A <see cref="NatsStreamPublication"/> yields a <see cref="NatsStreamMessageProducer"/> that persists messages
/// in a JetStream stream; any other <see cref="NatsPublication"/> yields a <see cref="NatsMessageProducer"/> over
/// core NATS. For stream publications the JetStream stream is created, validated, or assumed to exist according to
/// <see cref="Publication.MakeChannels"/>. When no explicit
/// <see cref="NatsStreamPublication.StreamConfiguration"/> is provided, the stream is named after the publication
/// topic with characters that are invalid in a stream name replaced by '-', and subscribes the topic as its only
/// subject; consumers must use that same stream name as their channel name.
/// </remarks>
/// <param name="client">The <see cref="INatsClient"/> used for core NATS publications.</param>
/// <param name="jsContext">The <see cref="INatsJSContext"/> used for JetStream publications.</param>
/// <param name="publications">The <see cref="NatsPublication"/> set to create producers for.</param>
/// <param name="instrumentation">The <see cref="InstrumentationOptions"/> controlling how much telemetry is written.</param>
public partial class NatsMessageProducerFactory(
    INatsClient client,
    INatsJSContext jsContext,
    IEnumerable<NatsPublication> publications,
    InstrumentationOptions instrumentation) : IAmAMessageProducerFactory
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsMessageProducerFactory>();

    /// <summary>
    /// Creates a producer for each publication, keyed by topic and request type.
    /// </summary>
    /// <returns>A <see cref="Dictionary{TKey,TValue}"/> of <see cref="ProducerKey"/> to <see cref="IAmAMessageProducer"/>.</returns>
    /// <exception cref="ChannelFailureException">Thrown when a stream publication is validated but its JetStream stream does not exist.</exception>
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        return BrighterAsyncContext.Run(async () => await CreateAsync());
    }

    /// <summary>
    /// Asynchronously creates a producer for each publication, keyed by topic and request type.
    /// </summary>
    /// <returns>A <see cref="Dictionary{TKey,TValue}"/> of <see cref="ProducerKey"/> to <see cref="IAmAMessageProducer"/>.</returns>
    /// <exception cref="ChannelFailureException">Thrown when a stream publication is validated but its JetStream stream does not exist.</exception>
    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var publicationsByTopic = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                continue;
            }

            if (publication is NatsStreamPublication streamPublication)
            {
                await EnsureExistsAsync(streamPublication);
                Log.CreatingStreamProducer(s_logger, streamPublication.Topic!.Value);
                publicationsByTopic[new ProducerKey(publication.Topic, publication.Type)] =
                    new NatsStreamMessageProducer(jsContext, streamPublication, instrumentation);
            }
            else
            {
                Log.CreatingCoreProducer(s_logger, publication.Topic!.Value);
                publicationsByTopic[new ProducerKey(publication.Topic, publication.Type)] =
                    new NatsMessageProducer(client, publication, instrumentation);
            }
        }

        return publicationsByTopic;
    }


    private async Task EnsureExistsAsync(NatsStreamPublication publication)
    {
        if (publication.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            try
            {
                _ = await jsContext.GetStreamAsync(publication.StreamConfiguration?.Name ?? NatsNameSanitizer.Sanitize(publication.Topic!.Value));
            }
            catch (NatsJSApiException e) when (e.Error.Code == 404)
            {
                Log.StreamMissing(s_logger, publication.Topic!.Value);
                throw new ChannelFailureException(
                    $"Stream for topic {publication.Topic!.Value} does not exist", e);
            }

            return;
        }

        var config = publication.StreamConfiguration ?? new StreamConfig
        {
            Name = NatsNameSanitizer.Sanitize(publication.Topic!.Value),
            Subjects = [publication.Topic!.Value]
        };
        Log.CreatingOrUpdatingStream(s_logger, config.Name ?? publication.Topic!.Value);
        await jsContext.CreateOrUpdateStreamAsync(config);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Creating core NATS producer for subject {Subject}")]
        public static partial void CreatingCoreProducer(ILogger logger, string subject);

        [LoggerMessage(LogLevel.Debug, "Creating JetStream producer for subject {Subject}")]
        public static partial void CreatingStreamProducer(ILogger logger, string subject);

        [LoggerMessage(LogLevel.Warning, "JetStream stream for topic {Topic} does not exist")]
        public static partial void StreamMissing(ILogger logger, string topic);

        [LoggerMessage(LogLevel.Debug, "Creating or updating JetStream stream {StreamName}")]
        public static partial void CreatingOrUpdatingStream(ILogger logger, string streamName);
    }
}
