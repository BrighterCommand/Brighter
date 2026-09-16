#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// Provisioning turns an unexpected provider error into a ConfigurationException naming the queue
/// table and the MakeChannels setting that avoids it. That is right for a permission denial and
/// wrong for a failure that will pass on its own: told to reconfigure because the database was
/// throttled for four seconds, an application's author fixes the wrong thing, and any policy that
/// retries on SqlException.Number stops seeing the error at all.
///
/// So a transient error is left exactly as the provider threw it. The pair below is what makes that
/// claim mean something — the same code path, one error that must stay raw and one that must be
/// wrapped. Without the second, a gateway that had stopped wrapping anything would pass.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningTransientTests : IDisposable
{
    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private SqlConnection? _blocker;

    public MsSqlQueueProvisioningTransientTests() =>
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);

    [Fact]
    public void When_provisioning_hits_a_transient_failure_should_leave_it_as_the_provider_threw_it()
    {
        //Arrange -- a timeout provoked by a lock rather than by a clock, so this is deterministic
        //rather than a race the test hopes to win. The queue table exists but has no topic index,
        //and another session holds it under TABLOCKX inside an open transaction: the IF NOT EXISTS
        //guard passes, and CREATE NONCLUSTERED INDEX blocks on the schema-modify lock until the
        //one second command timeout expires. Measured: SqlException -2, "Execution Timeout Expired".
        CreateQueueTableWithoutIndex(_queueTable);
        HoldExclusiveLock(_queueTable);

        var configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString + ";Command Timeout=1", queueStoreTable: _queueTable);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(Subscription()));

        //Assert -- raw, and specifically not the ConfigurationException a permission denial gets.
        var sqlException = Assert.IsType<SqlException>(exception);
        Assert.Equal(-2, sqlException.Number);
    }

    [Fact]
    public void When_provisioning_hits_a_permission_failure_should_still_wrap_it()
    {
        //Arrange -- the control, and the reason the fact above is not vacuous: an error that is not
        //transient, on the same code path, must still arrive as a ConfigurationException. A gateway
        //that wrapped nothing at all would satisfy the first fact on its own.
        //
        //A view holding the queue table's name is the cheapest non-transient failure to provoke: the
        //CREATE TABLE takes 2714, the swallow treats it as a lost race, and the re-probe throws.
        CreateView(_queueTable);
        var configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        try
        {
            //Act
            var exception = Record.Exception(() => channelFactory.CreateSyncChannel(Subscription()));

            //Assert
            Assert.IsType<ConfigurationException>(exception);
        }
        finally
        {
            DropView(_queueTable);
        }
    }

    [Fact]
    public void When_the_connect_itself_is_transient_should_leave_it_as_the_provider_threw_it()
    {
        //Arrange -- the same rule on the other path, and this is the path that matters most: of the
        //codes in the transient set, the majority are raised during login and can never come out of
        //a command. A rule applied only to the DDL would exempt them in the one place they cannot
        //appear and nowhere they can.
        //
        //A listener that accepts the TCP connection and then answers nothing: the pre-login
        //handshake never completes and SqlClient gives up on its connect timeout. Measured,
        //SqlException -2, "Connection Timeout Expired ... while attempting to consume the pre-login
        //handshake acknowledgement". Deterministic, in-process, and nothing to install.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var port = ((IPEndPoint)silent.LocalEndpoint).Port;
        _ = AcceptAndIgnore(silent);

        var configuration = new RelationalDatabaseConfiguration(
            $"Server=127.0.0.1,{port};Database=BrighterTests;User Id=sa;Password=Password123!;" +
            "Connect Timeout=2;Encrypt=false",
            queueStoreTable: _queueTable);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(Subscription()));

        //Assert
        var sqlException = Assert.IsType<SqlException>(exception);
        Assert.Equal(-2, sqlException.Number);
    }

    [Fact]
    public void When_the_connect_fails_for_a_reason_that_will_not_pass_should_wrap_it()
    {
        //Arrange -- the control for the fact above, and the reason 4060, 40615 and 11001 are
        //deliberately absent from the transient set: a server that is not there at all is the
        //commonest first-run mistake, and the message naming the queue table is worth most exactly
        //then. Measured, it arrives as number 0 rather than as a timeout, so it stays wrapped.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString, queueStoreTable: _queueTable);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(Subscription()));

        //Assert
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains(_queueTable, configurationException.Message);
    }

    private static async Task AcceptAndIgnore(TcpListener listener)
    {
        try
        {
            while (true)
            {
                //Held open rather than disposed: closing it would give the client a reset, which is
                //a different error from the silence this test is built on.
                _ = await listener.AcceptTcpClientAsync();
            }
        }
        catch (ObjectDisposedException)
        {
            //The test finished and disposed the listener.
        }
        catch (SocketException)
        {
            //Likewise.
        }
    }

    private static MsSqlSubscription<MyCommand> Subscription() =>
        new(new SubscriptionName("transient.subscription"),
            new ChannelName("transient.channel"),
            new RoutingKey("transient.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

    private void HoldExclusiveLock(string queueTable)
    {
        _blocker = new SqlConnection(Configuration.DefaultConnectingString);
        _blocker.Open();
        using var command = _blocker.CreateCommand();
        command.CommandText = $"BEGIN TRAN; SELECT * FROM [{queueTable}] WITH (TABLOCKX);";
        command.ExecuteNonQuery();
    }

    private static void CreateQueueTableWithoutIndex(string queueTable) =>
        Execute(MsSqlQueueBuilder.GetDDL(queueTable));

    private static void CreateView(string name) => Execute($"CREATE VIEW [{name}] AS SELECT 1 AS x");

    private static void DropView(string name) => Execute($"DROP VIEW IF EXISTS [{name}]");

    private static void Execute(string sql)
    {
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        //The lock has to go before the drop, or the drop blocks on it for the rest of the run.
        if (_blocker is not null)
        {
            using (var rollback = _blocker.CreateCommand())
            {
                rollback.CommandText = "IF @@TRANCOUNT > 0 ROLLBACK TRAN;";
                rollback.ExecuteNonQuery();
            }

            _blocker.Dispose();
            _blocker = null;
        }

        MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);
    }

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
