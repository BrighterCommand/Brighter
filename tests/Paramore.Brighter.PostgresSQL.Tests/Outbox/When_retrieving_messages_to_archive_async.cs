﻿using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgresSqlArchiveFetchAsyncTests : IDisposable
{
    private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly PostgreSqlOutbox _sqlOutbox;

    public PostgresSqlArchiveFetchAsyncTests()
    {
        _postgresSqlTestHelper = new PostgresSqlTestHelper();
        _postgresSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public async Task When_Retrieving_Messages_To_Archive_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var allDispatched =
            await _sqlOutbox.DispatchedMessagesAsync(0, context, cancellationToken: CancellationToken.None);
        var messagesOverAnHour =
            await _sqlOutbox.DispatchedMessagesAsync(1, context, cancellationToken: CancellationToken.None);
        var messagesOver4Hours =
            await _sqlOutbox.DispatchedMessagesAsync(4, context, cancellationToken: CancellationToken.None);

        //Assert
        allDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
    }

    [Fact]
    public async Task When_Retrieving_Messages_To_Archive_UsingTimeSpan_Async()
    {
        var context = new RequestContext();
        await _sqlOutbox.AddAsync([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        await _sqlOutbox.MarkDispatchedAsync(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await _sqlOutbox.MarkDispatchedAsync(_messageDispatched.Id, context);

        var allDispatched =
            await _sqlOutbox.DispatchedMessagesAsync(TimeSpan.Zero, context, 100, cancellationToken: CancellationToken.None);
        var messagesOverAnHour =
            await _sqlOutbox.DispatchedMessagesAsync(TimeSpan.FromHours(2), context, 100, cancellationToken: CancellationToken.None);
        var messagesOver4Hours =
            await _sqlOutbox.DispatchedMessagesAsync(TimeSpan.FromHours(4), context, 100, cancellationToken: CancellationToken.None);

        //Assert
        allDispatched.Should().HaveCount(2);
        messagesOverAnHour.Should().ContainSingle();
        messagesOver4Hours.Should().BeEmpty();
    }

    public void Dispose()
    {
        _postgresSqlTestHelper.CleanUpDb();
    }
}