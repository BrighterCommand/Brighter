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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests;

[Trait("Category", "MSSQL")]
public class MsSqlDatabaseSetupTests : IDisposable
{
    private readonly List<string> _databases = [];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_ensuring_a_database_should_create_it_once(bool useAsync)
    {
        //Arrange
        string database = NewDatabaseName();
        string connectionString = ConnectionString(database);

        //Act
        await EnsureDatabaseAsync(connectionString, useAsync);
        await EnsureDatabaseAsync(connectionString, useAsync);

        //Assert
        Assert.True(DatabaseExists(database));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_database_creation_fails_should_retry_instead_of_remembering_success(
        bool firstAsync, bool retryAsync)
    {
        //Arrange
        string invalidConnectionString = ConnectionString(new string('x', 129));
        string database = NewDatabaseName();

        //Act
        var firstError = await Record.ExceptionAsync(() => EnsureDatabaseAsync(invalidConnectionString, firstAsync));
        var retryError = await Record.ExceptionAsync(() => EnsureDatabaseAsync(invalidConnectionString, retryAsync));
        await EnsureDatabaseAsync(ConnectionString(database), retryAsync);

        //Assert
        Assert.IsType<SqlException>(firstError);
        Assert.IsType<SqlException>(retryError);
        Assert.True(DatabaseExists(database));
    }

    [Fact]
    public async Task When_ensuring_different_databases_should_create_each_one()
    {
        //Arrange
        string first = NewDatabaseName();
        string second = NewDatabaseName();

        //Act
        Configuration.EnsureDatabaseExists(ConnectionString(first));
        await Configuration.EnsureDatabaseExistsAsync(ConnectionString(second));

        //Assert
        Assert.True(DatabaseExists(first));
        Assert.True(DatabaseExists(second));
    }

    [Fact]
    public async Task When_sync_and_async_callers_create_the_same_database_should_all_succeed()
    {
        //Arrange
        string database = NewDatabaseName();
        string connectionString = ConnectionString(database);
        using var start = new Barrier(8);
        Task[] callers = Enumerable.Range(0, 8).Select(index => Task.Factory.StartNew(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Database setup callers did not reach the start barrier.");

            await EnsureDatabaseAsync(connectionString, useAsync: index % 2 == 0);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()).ToArray();

        //Act
        await Task.WhenAll(callers);

        //Assert
        Assert.True(DatabaseExists(database));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_database_name_contains_quotes_or_brackets_should_use_the_literal_name(bool useAsync)
    {
        //Arrange
        string database = NewDatabaseName("'bracket]");

        //Act
        await EnsureDatabaseAsync(ConnectionString(database), useAsync);

        //Assert
        Assert.True(DatabaseExists(database));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_database_setup_runs_inside_an_ambient_transaction_should_create_outside_it(bool useAsync)
    {
        //Arrange
        string database = NewDatabaseName();

        //Act
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await EnsureDatabaseAsync(ConnectionString(database), useAsync);
        }

        //Assert
        Assert.True(DatabaseExists(database));
    }

    private string NewDatabaseName(string suffix = "")
    {
        string database = "brighter_setup_" + Guid.NewGuid().ToString("N") + suffix;
        _databases.Add(database);
        return database;
    }

    private static string ConnectionString(string database) =>
        new SqlConnectionStringBuilder(Configuration.DefaultConnectingString)
        {
            InitialCatalog = database
        }.ConnectionString;

    private static Task EnsureDatabaseAsync(string connectionString, bool useAsync)
    {
        if (useAsync)
            return Configuration.EnsureDatabaseExistsAsync(connectionString);

        Configuration.EnsureDatabaseExists(connectionString);
        return Task.CompletedTask;
    }

    private static bool DatabaseExists(string database)
    {
        using var connection = new SqlConnection(ConnectionString("master"));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName";
        command.Parameters.AddWithValue("@databaseName", database);
        return (int)command.ExecuteScalar()! == 1;
    }

    public void Dispose()
    {
        using var connection = new SqlConnection(ConnectionString("master"));
        connection.Open();
        using var builder = new SqlCommandBuilder();
        foreach (string database in _databases)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS {builder.QuoteIdentifier(database)}";
            command.ExecuteNonQuery();
        }
    }
}
