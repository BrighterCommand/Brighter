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
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestHelpers;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Property("Category", "Sqlite")]
public class SqliteOutboxDuplicateConstraintTests
{
    [Test]
    [Arguments("PRIMARY KEY", false, false, false)]
    [Arguments("PRIMARY KEY", false, false, true)]
    [Arguments("PRIMARY KEY", false, true, false)]
    [Arguments("PRIMARY KEY", false, true, true)]
    [Arguments("PRIMARY KEY", true, false, false)]
    [Arguments("PRIMARY KEY", true, false, true)]
    [Arguments("PRIMARY KEY", true, true, false)]
    [Arguments("PRIMARY KEY", true, true, true)]
    [Arguments("UNIQUE", false, false, false)]
    [Arguments("UNIQUE", false, false, true)]
    [Arguments("UNIQUE", false, true, false)]
    [Arguments("UNIQUE", false, true, true)]
    [Arguments("UNIQUE", true, false, false)]
    [Arguments("UNIQUE", true, false, true)]
    [Arguments("UNIQUE", true, true, false)]
    [Arguments("UNIQUE", true, true, true)]
    public async Task When_sqlite_outbox_receives_a_duplicate_should_keep_one_row(
        string constraint, bool binary, bool useAsync, bool useBulk)
    {
        //Arrange
        using var database = new SqliteConstraintDatabase(inbox: false, binary: binary, constraint: constraint);
        var outbox = new SqliteOutbox(database.Configuration);
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("test-topic"), MessageType.MT_EVENT),
            new MessageBody("original"));
        var context = new RequestContext();

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            for (var attempt = 0; attempt < 2; attempt++)
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
            }
        });

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(database.RowCount()).IsEqualTo(1L);
        await Assert.That(outbox.Get(message.Id, context).Body.Value).IsEqualTo("original");
    }
}
