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
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class OutstandingCountWithoutMessageBodiesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteOutbox _outbox;

    public OutstandingCountWithoutMessageBodiesTests()
    {
        var connectionString = $"Data Source={Uuid.New():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE messages (Timestamp TEXT NOT NULL, Dispatched TEXT NULL);
            INSERT INTO messages VALUES (@Old, NULL), (@Old, NULL), (@Old, NULL),
                (@Recent, NULL), (@Old, @Recent);
            CREATE VIEW outbox AS
                SELECT Timestamp, Dispatched, json('unreadable body') AS Body FROM messages;
            """;
        command.Parameters.AddWithValue("@Old", DateTimeOffset.UtcNow.AddMinutes(-10));
        command.Parameters.AddWithValue("@Recent", DateTimeOffset.UtcNow.AddMinutes(-1));
        command.ExecuteNonQuery();
        _outbox = new SqliteOutbox(new RelationalDatabaseConfiguration(connectionString, outBoxTableName: "outbox"));
    }

    [Theory]
    [InlineData(false, 100, 3)]
    [InlineData(true, 100, 3)]
    [InlineData(false, int.MaxValue, 3)]
    [InlineData(true, int.MaxValue, 3)]
    [InlineData(false, 2, 2)]
    [InlineData(true, 2, 2)]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 0)]
    public async Task When_counting_outstanding_messages_should_not_read_message_bodies(bool useAsync, int maxCount, int expected)
    {
        //Arrange
        var age = TimeSpan.FromMinutes(5);

        //Act
        var count = useAsync
            ? await _outbox.GetOutstandingMessageCountAsync(age, new RequestContext(), maxCount)
            : _outbox.GetOutstandingMessageCount(age, new RequestContext(), maxCount);

        //Assert
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_no_messages_are_old_enough_should_count_zero(bool useAsync)
    {
        //Arrange
        var age = TimeSpan.FromHours(1);

        //Act
        var count = useAsync
            ? await _outbox.GetOutstandingMessageCountAsync(age, new RequestContext())
            : _outbox.GetOutstandingMessageCount(age, new RequestContext());

        //Assert
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_counting_with_a_ceiling_should_stop_reading_rows_at_the_ceiling(bool useAsync)
    {
        //Arrange
        using var command = _connection.CreateCommand();
        command.CommandText = """
            DROP VIEW outbox;
            CREATE VIEW outbox AS
                SELECT CASE WHEN rowid <= 2 THEN Timestamp ELSE json('read beyond ceiling') END AS Timestamp,
                    Dispatched, json('unreadable body') AS Body FROM messages;
            """;
        command.ExecuteNonQuery();

        //Act
        var count = useAsync
            ? await _outbox.GetOutstandingMessageCountAsync(TimeSpan.FromMinutes(5), new RequestContext(), 2)
            : _outbox.GetOutstandingMessageCount(TimeSpan.FromMinutes(5), new RequestContext(), 2);

        //Assert
        Assert.Equal(2, count);
    }

    public void Dispose() => _connection.Dispose();
}
