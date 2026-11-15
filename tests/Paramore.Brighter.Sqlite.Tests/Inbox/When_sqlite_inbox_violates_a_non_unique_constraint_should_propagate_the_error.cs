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

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Property("Category", "Sqlite")]
public class SqliteInboxNonUniqueConstraintTests
{
    [Test]
    [Arguments("CHECK", 275, false, false)]
    [Arguments("CHECK", 275, false, true)]
    [Arguments("CHECK", 275, true, false)]
    [Arguments("CHECK", 275, true, true)]
    [Arguments("NOT NULL", 1299, false, false)]
    [Arguments("NOT NULL", 1299, false, true)]
    [Arguments("NOT NULL", 1299, true, false)]
    [Arguments("NOT NULL", 1299, true, true)]
    [Arguments("FOREIGN KEY", 787, false, false)]
    [Arguments("FOREIGN KEY", 787, false, true)]
    [Arguments("FOREIGN KEY", 787, true, false)]
    [Arguments("FOREIGN KEY", 787, true, true)]
    [Arguments("TRIGGER", 1811, false, false)]
    [Arguments("TRIGGER", 1811, false, true)]
    [Arguments("TRIGGER", 1811, true, false)]
    [Arguments("TRIGGER", 1811, true, true)]
    public async Task When_sqlite_inbox_violates_a_non_unique_constraint_should_propagate_the_error(
        string constraint, int expectedExtendedCode, bool binary, bool useAsync)
    {
        //Arrange
        using var database = new SqliteConstraintDatabase(inbox: true, binary: binary, constraint: constraint);
        var inbox = new SqliteInbox(database.Configuration);
        var command = new MyCommand { Value = "original" };
        var context = new RequestContext();

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            if (useAsync)
                await inbox.AddAsync(command, "test-context", context, -1, CancellationToken.None);
            else
                inbox.Add(command, "test-context", context, -1);
        });

        //Assert
        var sqliteException = await Assert.That(exception).IsTypeOf<SqliteException>();
        await Assert.That(sqliteException.SqliteErrorCode).IsEqualTo(19);
        await Assert.That(sqliteException.SqliteExtendedErrorCode).IsEqualTo(expectedExtendedCode);
        await Assert.That(database.RowCount()).IsEqualTo(0L);
    }
}
