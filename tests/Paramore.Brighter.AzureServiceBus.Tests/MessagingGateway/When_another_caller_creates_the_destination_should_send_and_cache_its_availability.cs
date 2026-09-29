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


namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusProducerCreationRaceTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        await Assert.That(sender.SentMessages.Count).IsEqualTo(2);
        await Assert.That(sender.SentMessages[0].MessageId).IsEqualTo(first.Id.Value);
        await Assert.That(sender.SentMessages[1].MessageId).IsEqualTo(second.Id.Value);
        await Assert.That(administration.ExistsCount).IsEqualTo(1);
        await Assert.That(administration.CreateCount).IsEqualTo(1);
        await Assert.That(administration.ResetCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That((await Assert.That(firstSender.SentMessages).HasSingleItem()).MessageId).IsEqualTo(first.Id.Value);
        await Assert.That((await Assert.That(secondSender.SentMessages).HasSingleItem()).MessageId).IsEqualTo(second.Id.Value);
        await Assert.That(administration.ExistsCount).IsEqualTo(2);
        await Assert.That(administration.CreateCount).IsEqualTo(2);
        await Assert.That(administration.ResetCount).IsEqualTo(0);
    }

    public static IEnumerable<object[]> AdministrationFailures()
    {
        foreach (var useQueue in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        foreach (var duringCreation in new[] { false, true })
        foreach (var failureKind in new[] { 0, 1, 2, 3 })
            yield return [useQueue, useAsync, duringCreation, failureKind];
    }

    [Test]
    [MethodDataSource(nameof(AdministrationFailures))]
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
        var actual = await TestExceptionRecorder.CaptureAsync(() => SendAsync(producer, message, useAsync));

        //Assert
        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(sender.SentMessages).IsEmpty();
        await Assert.That(administration.ResetCount).IsEqualTo(1);
        await Assert.That(administration.CreateCount).IsEqualTo(duringCreation ? 1 : 0);

        //Act
        administration.ExistsException = null;
        administration.CreateException = null;
        await SendAsync(producer, message, useAsync);

        //Assert
        await Assert.That((await Assert.That(sender.SentMessages).HasSingleItem()).MessageId).IsEqualTo(message.Id.Value);
        await Assert.That(administration.ExistsCount).IsEqualTo(2);
        await Assert.That(administration.CreateCount).IsEqualTo(duringCreation ? 2 : 1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        var actual = await TestExceptionRecorder.CaptureAsync(() => SendAsync(producer, CreateMessage(), useAsync));

        //Assert
        await Assert.That(actual).IsSameReferenceAs(expected);
        await Assert.That(sender.SentMessages).IsEmpty();
        await Assert.That(administration.CreateCount).IsEqualTo(0);
        await Assert.That(administration.ResetCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false, OnMissingChannel.Assume)]
    [Arguments(false, true, OnMissingChannel.Assume)]
    [Arguments(true, false, OnMissingChannel.Assume)]
    [Arguments(true, true, OnMissingChannel.Assume)]
    [Arguments(false, false, OnMissingChannel.Validate)]
    [Arguments(false, true, OnMissingChannel.Validate)]
    [Arguments(true, false, OnMissingChannel.Validate)]
    [Arguments(true, true, OnMissingChannel.Validate)]
    public async Task When_creation_is_not_requested_should_preserve_the_missing_channel_policy(
        bool useQueue, bool useAsync, OnMissingChannel mode)
    {
        //Arrange
        var administration = new InMemoryRacingAdministrationClient();
        var sender = new FakeServiceBusSenderWrapper();
        using var producer = CreateProducer(useQueue, administration, sender, mode);
        var message = CreateMessage();

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => SendAsync(producer, message, useAsync));

        //Assert
        await Assert.That(administration.CreateCount).IsEqualTo(0);
        if (mode == OnMissingChannel.Assume)
        {
            await Assert.That(exception).IsNull();
            await Assert.That((await Assert.That(sender.SentMessages).HasSingleItem()).MessageId).IsEqualTo(message.Id.Value);
            await Assert.That(administration.ExistsCount).IsEqualTo(0);
            await Assert.That(administration.ResetCount).IsEqualTo(0);
        }
        else
        {
            await Assert.That(exception).IsTypeOf<ChannelFailureException>();
            await Assert.That(sender.SentMessages).IsEmpty();
            await Assert.That(administration.ExistsCount).IsEqualTo(1);
            await Assert.That(administration.ResetCount).IsEqualTo(1);
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
