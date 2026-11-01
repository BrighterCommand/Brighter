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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class SqliteOutboxNonUniqueConstraintTests
{
    [Theory]
    [InlineData("CHECK", 275, false, false, false)]
    [InlineData("CHECK", 275, false, false, true)]
    [InlineData("CHECK", 275, false, true, false)]
    [InlineData("CHECK", 275, false, true, true)]
    [InlineData("CHECK", 275, true, false, false)]
    [InlineData("CHECK", 275, true, false, true)]
    [InlineData("CHECK", 275, true, true, false)]
    [InlineData("CHECK", 275, true, true, true)]
    [InlineData("NOT NULL", 1299, false, false, false)]
    [InlineData("NOT NULL", 1299, false, false, true)]
    [InlineData("NOT NULL", 1299, false, true, false)]
    [InlineData("NOT NULL", 1299, false, true, true)]
    [InlineData("NOT NULL", 1299, true, false, false)]
    [InlineData("NOT NULL", 1299, true, false, true)]
    [InlineData("NOT NULL", 1299, true, true, false)]
    [InlineData("NOT NULL", 1299, true, true, true)]
    [InlineData("FOREIGN KEY", 787, false, false, false)]
    [InlineData("FOREIGN KEY", 787, false, false, true)]
    [InlineData("FOREIGN KEY", 787, false, true, false)]
    [InlineData("FOREIGN KEY", 787, false, true, true)]
    [InlineData("FOREIGN KEY", 787, true, false, false)]
    [InlineData("FOREIGN KEY", 787, true, false, true)]
    [InlineData("FOREIGN KEY", 787, true, true, false)]
    [InlineData("FOREIGN KEY", 787, true, true, true)]
    [InlineData("TRIGGER", 1811, false, false, false)]
    [InlineData("TRIGGER", 1811, false, false, true)]
    [InlineData("TRIGGER", 1811, false, true, false)]
    [InlineData("TRIGGER", 1811, false, true, true)]
    [InlineData("TRIGGER", 1811, true, false, false)]
    [InlineData("TRIGGER", 1811, true, false, true)]
    [InlineData("TRIGGER", 1811, true, true, false)]
    [InlineData("TRIGGER", 1811, true, true, true)]
    public async Task When_sqlite_outbox_violates_a_non_unique_constraint_should_propagate_the_error(
        string constraint, int expectedExtendedCode, bool binary, bool useAsync, bool useBulk)
    {
        //Arrange
        using var database = new SqliteConstraintDatabase(inbox: false, binary: binary, constraint: constraint);
        var outbox = new SqliteOutbox(database.Configuration);
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("test-topic"), MessageType.MT_EVENT),
            new MessageBody("original"));
        var context = new RequestContext();

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            if (useAsync)
            {
                if (useBulk)
                    await outbox.AddAsync(new[] { message }, context);
                else
                    await outbox.AddAsync(message, context);
            }
            else
            {
                if (useBulk)
                    outbox.Add(new[] { message }, context);
                else
                    outbox.Add(message, context);
            }
        });

        //Assert
        var sqliteException = Assert.IsType<SqliteException>(exception);
        Assert.Equal(19, sqliteException.SqliteErrorCode);
        Assert.Equal(expectedExtendedCode, sqliteException.SqliteExtendedErrorCode);
        Assert.Equal(0L, database.RowCount());
    }
}
