#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusProducerCreationRaceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_another_caller_creates_the_destination_should_send_and_cache_its_availability(
        bool useQueue, bool useAsync)
    {
        //Arrange
        var administration = new InMemoryRacingAdministrationClient { CreateByAnotherCallerAfterCheck = true };
        var sender = new FakeServiceBusSenderWrapper();
        using var producer = CreateProducer(useQueue, administration, sender);
        var first = CreateMessage();
        var second = CreateMessage();

        //Act
        await SendAsync(producer, first, useAsync);
        await SendAsync(producer, second, useAsync);

        //Assert
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(first.Id.Value, sender.SentMessages[0].MessageId);
        Assert.Equal(second.Id.Value, sender.SentMessages[1].MessageId);
        Assert.Equal(1, administration.ExistsCount);
        Assert.Equal(1, administration.CreateCount);
        Assert.Equal(0, administration.ResetCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_two_producers_race_to_create_the_destination_should_send_both_messages(bool useQueue)
    {
        //Arrange
        var administration = new InMemoryRacingAdministrationClient(participants: 2);
        var firstSender = new FakeServiceBusSenderWrapper();
        var secondSender = new FakeServiceBusSenderWrapper();
        using var firstProducer = CreateProducer(useQueue, administration, firstSender);
        using var secondProducer = CreateProducer(useQueue, administration, secondSender);
        var first = CreateMessage();
        var second = CreateMessage();

        //Act
        await Task.WhenAll(firstProducer.SendAsync(first), secondProducer.SendAsync(second))
            .WaitAsync(TimeSpan.FromSeconds(15));

        //Assert
        Assert.Equal(first.Id.Value, Assert.Single(firstSender.SentMessages).MessageId);
        Assert.Equal(second.Id.Value, Assert.Single(secondSender.SentMessages).MessageId);
        Assert.Equal(2, administration.ExistsCount);
        Assert.Equal(2, administration.CreateCount);
        Assert.Equal(0, administration.ResetCount);
    }

    public static IEnumerable<object[]> AdministrationFailures()
    {
        foreach (var useQueue in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        foreach (var duringCreation in new[] { false, true })
        foreach (var failureKind in new[] { 0, 1, 2, 3 })
            yield return [useQueue, useAsync, duringCreation, failureKind];
    }

    [Theory]
    [MemberData(nameof(AdministrationFailures))]
    public async Task When_administration_fails_should_propagate_the_error_and_allow_a_later_retry(
        bool useQueue, bool useAsync, bool duringCreation, int failureKind)
    {
        //Arrange
        Exception expected = failureKind switch
        {
            0 => new ServiceBusException("The service timed out.", ServiceBusFailureReason.ServiceTimeout),
            1 => new RequestFailedException(409, "A conflicting operation is still in progress."),
            2 => new UnauthorizedAccessException("Access denied."),
            _ => new OperationCanceledException("The administration operation was cancelled.")
        };
        var administration = new InMemoryRacingAdministrationClient
        {
            ExistsException = duringCreation ? null : expected,
            CreateException = duringCreation ? expected : null
        };
        var sender = new FakeServiceBusSenderWrapper();
        using var producer = CreateProducer(useQueue, administration, sender);
        var message = CreateMessage();

        //Act
        var actual = await Record.ExceptionAsync(() => SendAsync(producer, message, useAsync));

        //Assert
        Assert.Same(expected, actual);
        Assert.Empty(sender.SentMessages);
        Assert.Equal(1, administration.ResetCount);
        Assert.Equal(duringCreation ? 1 : 0, administration.CreateCount);

        //Act
        administration.ExistsException = null;
        administration.CreateException = null;
        await SendAsync(producer, message, useAsync);

        //Assert
        Assert.Equal(message.Id.Value, Assert.Single(sender.SentMessages).MessageId);
        Assert.Equal(2, administration.ExistsCount);
        Assert.Equal(duringCreation ? 2 : 1, administration.CreateCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_an_existence_check_reports_already_exists_should_not_treat_it_as_a_successful_create(
        bool useQueue, bool useAsync)
    {
        //Arrange
        var expected = new ServiceBusException("Unexpected existence-check failure.",
            ServiceBusFailureReason.MessagingEntityAlreadyExists);
        var administration = new InMemoryRacingAdministrationClient { ExistsException = expected };
        var sender = new FakeServiceBusSenderWrapper();
        using var producer = CreateProducer(useQueue, administration, sender);

        //Act
        var actual = await Record.ExceptionAsync(() => SendAsync(producer, CreateMessage(), useAsync));

        //Assert
        Assert.Same(expected, actual);
        Assert.Empty(sender.SentMessages);
        Assert.Equal(0, administration.CreateCount);
        Assert.Equal(1, administration.ResetCount);
    }

    [Theory]
    [InlineData(false, false, OnMissingChannel.Assume)]
    [InlineData(false, true, OnMissingChannel.Assume)]
    [InlineData(true, false, OnMissingChannel.Assume)]
    [InlineData(true, true, OnMissingChannel.Assume)]
    [InlineData(false, false, OnMissingChannel.Validate)]
    [InlineData(false, true, OnMissingChannel.Validate)]
    [InlineData(true, false, OnMissingChannel.Validate)]
    [InlineData(true, true, OnMissingChannel.Validate)]
    public async Task When_creation_is_not_requested_should_preserve_the_missing_channel_policy(
        bool useQueue, bool useAsync, OnMissingChannel mode)
    {
        //Arrange
        var administration = new InMemoryRacingAdministrationClient();
        var sender = new FakeServiceBusSenderWrapper();
        using var producer = CreateProducer(useQueue, administration, sender, mode);
        var message = CreateMessage();

        //Act
        var exception = await Record.ExceptionAsync(() => SendAsync(producer, message, useAsync));

        //Assert
        Assert.Equal(0, administration.CreateCount);
        if (mode == OnMissingChannel.Assume)
        {
            Assert.Null(exception);
            Assert.Equal(message.Id.Value, Assert.Single(sender.SentMessages).MessageId);
            Assert.Equal(0, administration.ExistsCount);
            Assert.Equal(0, administration.ResetCount);
        }
        else
        {
            Assert.IsType<ChannelFailureException>(exception);
            Assert.Empty(sender.SentMessages);
            Assert.Equal(1, administration.ExistsCount);
            Assert.Equal(1, administration.ResetCount);
        }
    }

    private static AzureServiceBusMessageProducer CreateProducer(bool useQueue,
        InMemoryRacingAdministrationClient administration, FakeServiceBusSenderWrapper sender,
        OnMissingChannel mode = OnMissingChannel.Create)
    {
        var publication = new AzureServiceBusPublication { MakeChannels = mode };
        var provider = new FakeServiceBusSenderProvider(sender);
        return useQueue
            ? new AzureServiceBusQueueMessageProducer(administration, provider, publication)
            : new AzureServiceBusTopicMessageProducer(administration, provider, publication);
    }

    private static async Task SendAsync(AzureServiceBusMessageProducer producer, Message message, bool useAsync)
    {
        if (useAsync)
            await producer.SendAsync(message);
        else
            producer.Send(message);
    }

    private static Message CreateMessage() => new(
        new MessageHeader(Id.Random(), new RoutingKey("creation-race"), MessageType.MT_EVENT),
        new MessageBody("test content"));
}
