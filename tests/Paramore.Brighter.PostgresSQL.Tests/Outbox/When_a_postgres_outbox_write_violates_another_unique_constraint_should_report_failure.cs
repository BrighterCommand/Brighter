#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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

using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Trait("Category", "PostgresSql")]
public class PostgreSqlOutboxUniqueConstraintFailureTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    public async Task When_a_postgres_outbox_write_violates_another_unique_constraint_should_report_failure(
        bool useAsync, bool binaryPayload, bool hasCausation, bool useBatch)
    {
        //Arrange
        using var store = new PostgreSqlOutboxTestStore(binaryPayload, hasCausation);
        store.Execute($"ALTER TABLE \"{store.TableName}\" ADD CONSTRAINT unique_topic UNIQUE (topic)");
        var outbox = new PostgreSqlOutbox(store.Configuration);
        var context = new RequestContext();
        var original = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "existing", "same-topic");
        outbox.Add(original, context);
        var conflicting = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "conflicting", "same-topic");
        var fresh = PostgreSqlOutboxTestStore.MessageWith(Id.Random(), "new", "new-topic");

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            if (useAsync)
            {
                if (useBatch)
                    await outbox.AddAsync(new[] { fresh, conflicting }, context);
                else
                    await outbox.AddAsync(conflicting, context);
            }
            else
            {
                if (useBatch)
                    outbox.Add(new[] { fresh, conflicting }, context);
                else
                    outbox.Add(conflicting, context);
            }
        });

        //Assert
        var postgresException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("unique_topic", postgresException.ConstraintName);
        Assert.Equal(1L, store.Count());
        Assert.Equal(original.Body.Value, outbox.Get(original.Id, context).Body.Value);
    }
}
