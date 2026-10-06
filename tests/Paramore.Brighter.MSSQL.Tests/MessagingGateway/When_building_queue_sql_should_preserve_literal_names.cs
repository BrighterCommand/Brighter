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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
public class MsSqlQueueBuilderLiteralNamesTests : IDisposable
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;

    public MsSqlQueueBuilderLiteralNamesTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _connection = new SqlConnection(Configuration.DefaultConnectingString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();
    }

    [Theory]
    [InlineData("queue-with-hyphens")]
    [InlineData("customer's queue")]
    [InlineData("queue]name")]
    [InlineData("queue]; SELECT 99;--")]
    [InlineData("queue]]; SELECT 99;--")]
    [InlineData("customer's.[queue]")]
    [InlineData("配送キュー")]
    public void When_building_queue_sql_should_preserve_literal_names(string name)
    {
        //Arrange
        string table = name + Guid.NewGuid().ToString("N");

        //Act
        Execute(MsSqlQueueBuilder.GetDDL(table));
        Execute(MsSqlQueueBuilder.GetIndexDDL(table));
        object? exists = Scalar(MsSqlQueueBuilder.GetExistsQuery(table));
        object? missing = Scalar(MsSqlQueueBuilder.GetExistsQuery(table + "_missing"));

        //Assert
        Assert.Equal(1, exists);
        Assert.Equal(0, missing);
        using var command = new SqlCommand(
            """
            SELECT COUNT(*) FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name = @table AND i.name = @index
            """,
            _connection, _transaction);
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@index", $"IX_{table}_Topic");
        Assert.Equal(1, command.ExecuteScalar());
    }

    [Theory]
    [InlineData("customer's queue", "owner's schema")]
    [InlineData("配送キュー", "配送スキーマ")]
    [InlineData("queue' OR 1=1 --", "schema' OR 1=1 --")]
    public void When_checking_queue_existence_should_match_the_literal_table_and_schema(string table, string schema)
    {
        //Arrange
        schema += Guid.NewGuid().ToString("N");
        using var builder = new SqlCommandBuilder();
        Execute($"CREATE SCHEMA {builder.QuoteIdentifier(schema)}");
        Execute($"CREATE TABLE {builder.QuoteIdentifier(schema)}.{builder.QuoteIdentifier(table)} (Id int)");

        //Act
        object? exists = Scalar(MsSqlQueueBuilder.GetExistsQuery(table, schema));
        object? missingTable = Scalar(MsSqlQueueBuilder.GetExistsQuery(table + "_missing", schema));
        object? missingSchema = Scalar(MsSqlQueueBuilder.GetExistsQuery(table, schema + "_missing"));

        //Assert
        Assert.Equal(1, exists);
        Assert.Equal(0, missingTable);
        Assert.Equal(0, missingSchema);
    }

    [Fact]
    public void When_checking_queue_existence_with_a_null_schema_should_use_the_callers_default_schema()
    {
        //Arrange
        string schema = "schema_" + Guid.NewGuid().ToString("N");
        string user = "user_" + Guid.NewGuid().ToString("N");
        string table = "queue_" + Guid.NewGuid().ToString("N");
        Execute($"CREATE SCHEMA [{schema}]");
        Execute($"CREATE USER [{user}] WITHOUT LOGIN WITH DEFAULT_SCHEMA = [{schema}]");
        Execute($"GRANT CREATE TABLE TO [{user}]");
        Execute($"GRANT CONTROL ON SCHEMA::[{schema}] TO [{user}]");
        Execute($"EXECUTE AS USER = '{user}'");
        try
        {
            //Act
            Execute(MsSqlQueueBuilder.GetDDL(table));
            Execute(MsSqlQueueBuilder.GetIndexDDL(table));
            object? exists = Scalar(MsSqlQueueBuilder.GetExistsQuery(table, schemaName: null));
            object? defaultDbo = Scalar(MsSqlQueueBuilder.GetExistsQuery(table));
            object? explicitDbo = Scalar(MsSqlQueueBuilder.GetExistsQuery(table, "dbo"));

            //Assert
            Assert.Equal(1, exists);
            Assert.Equal(0, defaultDbo);
            Assert.Equal(0, explicitDbo);
        }
        finally
        {
            Execute("REVERT");
        }
    }

    [Fact]
    public void When_checking_a_schema_at_the_identifier_limit_should_find_the_table()
    {
        //Arrange
        string schema = "schema_" + Guid.NewGuid().ToString("N") + new string('\'', 89);
        Assert.Equal(128, schema.Length);
        using var builder = new SqlCommandBuilder();
        Execute($"CREATE SCHEMA {builder.QuoteIdentifier(schema)}");
        Execute($"CREATE TABLE {builder.QuoteIdentifier(schema)}.[queue] (Id int)");

        //Act
        object? exists = Scalar(MsSqlQueueBuilder.GetExistsQuery("queue", schema));

        //Assert
        Assert.Equal(1, exists);
    }

    [Fact]
    public void When_building_a_table_at_the_identifier_limit_should_create_it()
    {
        //Arrange
        string table = "queue_" + Guid.NewGuid().ToString("N") + new string(']', 90);
        Assert.Equal(128, table.Length);

        //Act
        Execute(MsSqlQueueBuilder.GetDDL(table));

        //Assert
        Assert.Equal(1, Scalar(MsSqlQueueBuilder.GetExistsQuery(table)));
    }

    [Fact]
    public void When_building_an_index_at_the_identifier_limit_should_create_it()
    {
        //Arrange
        string table = "queue_" + Guid.NewGuid().ToString("N") + new string(']', 81);
        Assert.Equal(119, table.Length);
        Execute(MsSqlQueueBuilder.GetDDL(table));

        //Act
        Execute(MsSqlQueueBuilder.GetIndexDDL(table));

        //Assert
        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = @index", _connection, _transaction);
        command.Parameters.AddWithValue("@index", $"IX_{table}_Topic");
        Assert.Equal(1, command.ExecuteScalar());
    }

    private void Execute(string sql)
    {
        using var command = new SqlCommand(sql, _connection, _transaction);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var command = new SqlCommand(sql, _connection, _transaction);
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        _transaction.Dispose();
        _connection.Dispose();
    }
}
