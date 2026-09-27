#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System.Net;
using System.Text;
using System.Text.Json;
using System.Transactions;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.MessageScheduler.AWS.V4;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly.Registry;

namespace Paramore.Brighter.AWSScheduler.V4.Tests;

public class AwsScheduledPostContextTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_scheduling_a_post_should_preserve_context_metadata(bool isAsync, bool useDateTime)
    {
        //Arrange
        IAmACommandProcessor? processor = null;
        var http = new ScheduledRequestHttpClientFactory();
        var clientFactory = new AWSClientFactory(new BasicAWSCredentials("test-access-key", "test-secret-key"),
            RegionEndpoint.USEast1, config => config.HttpClientFactory = http);
        var scheduler = new AwsScheduler(clientFactory, TimeProvider.System, _ => Guid.NewGuid().ToString(),
            _ => Guid.NewGuid().ToString(), new Paramore.Brighter.MessageScheduler.AWS.V4.Scheduler
            {
                RoleArn = "arn:aws:iam::123456789012:role/test-scheduler",
                SchedulerTopic = new RoutingKey("arn:aws:sns:us-east-1:123456789012:test-scheduler")
            }, new SchedulerGroup { MakeSchedulerGroup = OnMissingSchedulerGroup.Assume });
        var factory = new RequestSchedulerFactory(scheduler);
        var bus = new InternalBus();
        var topic = new RoutingKey($"context-{Guid.NewGuid():N}");
        var publications = new[] { new Publication { Topic = topic, RequestType = typeof(MyCommand) } };
        var producerRegistry = new InMemoryProducerRegistryFactory(bus, publications, InstrumentationOptions.None).Create();
        using var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new CloudEventJsonMessageMapper<MyCommand>()),
            new SimpleMessageMapperFactoryAsync(_ => new CloudEventJsonMessageMapper<MyCommand>()));
        mappers.Register<MyCommand, CloudEventJsonMessageMapper<MyCommand>>();
        mappers.RegisterAsync<MyCommand, CloudEventJsonMessageMapper<MyCommand>>();
        using var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(), mappers,
            new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), null,
            new FindPublicationByPublicationTopicOrRequestType());
        var subscribers = new SubscriberRegistry();
        subscribers.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        subscribers.RegisterAsync<FireAwsScheduler, AwsSchedulerFiredHandler>();
        processor = new CommandProcessor(subscribers,
            new SimpleHandlerFactoryAsync(type => type == typeof(AwsSchedulerFiredHandler)
                ? new AwsSchedulerFiredHandler(processor!) : new FireSchedulerRequestHandler(processor!)),
            new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),
            mediator, factory);
        var context = new RequestContext();
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        var properties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = properties;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var request = new MyCommand();
        var delay = TimeSpan.FromSeconds(1);

        //Act
        if (isAsync)
        {
            if (useDateTime) await processor.PostAsync(DateTimeOffset.UtcNow.Add(delay), request, context);
            else await processor.PostAsync(delay, request, context);
        }
        else
        {
            if (useDateTime) processor.Post(DateTimeOffset.UtcNow.Add(delay), request, context);
            else processor.Post(delay, request, context);
        }
        headers["x-attempt"] = 4;
        properties["tenant"] = "changed";
        using var createSchedule = JsonDocument.Parse(Assert.Single(http.Requests));
        using var target = JsonDocument.Parse(createSchedule.RootElement.GetProperty("Target").GetProperty("Input").GetString()!);
        var envelope = JsonSerializer.Deserialize<FireAwsScheduler>(target.RootElement.GetProperty("Message").GetString()!,
            Paramore.Brighter.JsonConverters.JsonSerialisationOptions.Options)!;
        await processor.SendAsync(envelope);

        //Assert
        Assert.Equal(RequestSchedulerType.Post, envelope.SchedulerType);
        Assert.Equal(isAsync, envelope.Async);
        var message = Assert.Single(bus.Stream(topic));
        Assert.Equal(request.Id, message.Id);
        Assert.Equal(3, message.Header.Bag["x-attempt"]);
        Assert.Equal("partition-1", message.Header.PartitionKey.Value);
        using var json = JsonDocument.Parse(message.Body.Value);
        Assert.Equal("tenant-1", json.RootElement.GetProperty("tenant").GetString());
    }

    private sealed class RequestSchedulerFactory(AwsScheduler scheduler) : IAmARequestSchedulerFactory
    {
        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor) => scheduler;
        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor) => scheduler;
    }

    private sealed class ScheduledRequestHttpClientFactory : HttpClientFactory
    {
        public List<string> Requests { get; } = [];

        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new ScheduledRequestHandler(Requests));

        private sealed class ScheduledRequestHandler(List<string> requests) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.Contains("/schedules/", request.RequestUri!.AbsolutePath);
                requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ScheduleArn\":\"arn:aws:scheduler:us-east-1:123456789012:schedule/default/test\"}",
                        Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
