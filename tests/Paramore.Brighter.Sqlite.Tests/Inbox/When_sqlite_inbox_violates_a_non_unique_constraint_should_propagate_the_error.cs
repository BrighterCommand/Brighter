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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Trait("Category", "Sqlite")]
public class SqliteInboxNonUniqueConstraintTests
{
    [Theory]
    [InlineData("CHECK", 275, false, false)]
    [InlineData("CHECK", 275, false, true)]
    [InlineData("CHECK", 275, true, false)]
    [InlineData("CHECK", 275, true, true)]
    [InlineData("NOT NULL", 1299, false, false)]
    [InlineData("NOT NULL", 1299, false, true)]
    [InlineData("NOT NULL", 1299, true, false)]
    [InlineData("NOT NULL", 1299, true, true)]
    [InlineData("FOREIGN KEY", 787, false, false)]
    [InlineData("FOREIGN KEY", 787, false, true)]
    [InlineData("FOREIGN KEY", 787, true, false)]
    [InlineData("FOREIGN KEY", 787, true, true)]
    [InlineData("TRIGGER", 1811, false, false)]
    [InlineData("TRIGGER", 1811, false, true)]
    [InlineData("TRIGGER", 1811, true, false)]
    [InlineData("TRIGGER", 1811, true, true)]
    public async Task When_sqlite_inbox_violates_a_non_unique_constraint_should_propagate_the_error(
        string constraint, int expectedExtendedCode, bool binary, bool useAsync)
    {
        //Arrange
        using var database = new SqliteConstraintDatabase(inbox: true, binary: binary, constraint: constraint);
        var inbox = new SqliteInbox(database.Configuration);
        var command = new MyCommand { Value = "original" };
        var context = new RequestContext();

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            if (useAsync)
                await inbox.AddAsync(command, "test-context", context, -1, CancellationToken.None);
            else
                inbox.Add(command, "test-context", context, -1);
        });

        //Assert
        var sqliteException = Assert.IsType<SqliteException>(exception);
        Assert.Equal(19, sqliteException.SqliteErrorCode);
        Assert.Equal(expectedExtendedCode, sqliteException.SqliteExtendedErrorCode);
        Assert.Equal(0L, database.RowCount());
    }
}
