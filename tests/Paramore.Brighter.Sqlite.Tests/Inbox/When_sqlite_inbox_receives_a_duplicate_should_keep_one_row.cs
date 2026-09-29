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
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestHelpers;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Property("Category", "Sqlite")]
public class SqliteInboxDuplicateConstraintTests
{
    [Test]
    [Arguments("PRIMARY KEY", false, false)]
    [Arguments("PRIMARY KEY", false, true)]
    [Arguments("PRIMARY KEY", true, false)]
    [Arguments("PRIMARY KEY", true, true)]
    [Arguments("UNIQUE", false, false)]
    [Arguments("UNIQUE", false, true)]
    [Arguments("UNIQUE", true, false)]
    [Arguments("UNIQUE", true, true)]
    public async Task When_sqlite_inbox_receives_a_duplicate_should_keep_one_row(
        string constraint, bool binary, bool useAsync)
    {
        //Arrange
        using var database = new SqliteConstraintDatabase(inbox: true, binary: binary, constraint: constraint);
        var inbox = new SqliteInbox(database.Configuration);
        var command = new MyCommand { Value = "original" };
        var context = new RequestContext();

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (useAsync)
                    await inbox.AddAsync(command, "test-context", context, -1, CancellationToken.None);
                else
                    inbox.Add(command, "test-context", context, -1);
            }
        });

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(database.RowCount()).IsEqualTo(1L);
        await Assert.That(inbox.Get<MyCommand>(command.Id, "test-context", context, -1).Value).IsEqualTo("original");
    }
}
