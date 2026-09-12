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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// Create is the default, so the commonest way to meet the provisioner is by upgrading into it, and
/// the likeliest way for that to go wrong is not a lost race — it is an application login with DML
/// rights and no DDL rights. SQL Server answers that with error 262, "CREATE TABLE permission denied
/// in database", which is none of 2714, 1913 or 1205 and so is nobody's expected case.
///
/// The connect succeeds, so the wrapper around Connect never fires; without a wrapper on the DDL a
/// raw SqlException comes out of channel open with nothing in it naming MakeChannels. These tests
/// are about the sentence a surprised user meets.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningPermissionTests : IDisposable
{
    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly string _login = "brighter_no_ddl_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _readWriteConnectionString;
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningPermissionTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);

        var builder = new SqlConnectionStringBuilder(Configuration.DefaultConnectingString);

        Execute($"""
                 CREATE LOGIN [{_login}] WITH PASSWORD = 'Prob3_{_login}!', CHECK_POLICY = OFF;
                 CREATE USER [{_login}] FOR LOGIN [{_login}];
                 ALTER ROLE db_datareader ADD MEMBER [{_login}];
                 ALTER ROLE db_datawriter ADD MEMBER [{_login}];
                 """);

        builder.UserID = _login;
        builder.Password = $"Prob3_{_login}!";
        _readWriteConnectionString = builder.ConnectionString;
        _configuration = new RelationalDatabaseConfiguration(
            _readWriteConnectionString, queueStoreTable: _queueTable);
    }

    [Fact]
    public void When_the_login_cannot_create_the_queue_should_name_the_setting_that_avoids_it()
    {
        //Arrange
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(Subscription(OnMissingChannel.Create)));

        //Assert -- the provider's own sentence is kept, because it is the one that says what was
        //denied; what is added is the table and the way out.
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains(_queueTable, configurationException.Message);
        Assert.Contains("permission denied", configurationException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Validate", configurationException.Message);
        Assert.IsType<SqlException>(configurationException.InnerException);
    }

    [Fact]
    public void When_the_login_cannot_create_the_queue_but_it_exists_should_validate_without_ddl_rights()
    {
        //Arrange -- the control, and the reason the message names Validate rather than apologising:
        //the same login that cannot create the table can check that it is there. Created here as an
        //administrator, as a migration or a DBA would.
        Execute($"CREATE TABLE [{_queueTable}] ([Id] [BIGINT] IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "[Topic] [NVARCHAR](255) NOT NULL, [MessageType] [NVARCHAR](1024) NOT NULL, " +
                "[Payload] [NVARCHAR](MAX) NOT NULL)");
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));

        //Act
        var exception = Record.Exception(() =>
        {
            using var channel = channelFactory.CreateSyncChannel(Subscription(OnMissingChannel.Validate));
        });

        //Assert
        Assert.Null(exception);
    }

    private static MsSqlSubscription<MyCommand> Subscription(OnMissingChannel makeChannels) =>
        new(new SubscriptionName("permission.subscription"),
            new ChannelName("permission.channel"),
            new RoutingKey("permission.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannels);

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
        MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

        //A login cannot be dropped while a pooled connection is still open under it, and the
        //gateway's connections are pooled by definition.
        SqlConnection.ClearAllPools();
        Execute($"""
                 DECLARE @kill nvarchar(max) = N'';
                 SELECT @kill += 'KILL ' + CAST(session_id AS varchar(10)) + ';'
                 FROM sys.dm_exec_sessions WHERE login_name = '{_login}';
                 EXEC(@kill);
                 DROP USER IF EXISTS [{_login}];
                 DROP LOGIN [{_login}];
                 """);
    }

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
