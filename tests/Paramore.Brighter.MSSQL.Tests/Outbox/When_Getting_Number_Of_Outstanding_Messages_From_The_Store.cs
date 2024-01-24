using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Trait("Category", "MSSQL")]
public class When_Getting_Number_Of_Outstanding_Messages_From_The_Store : IDisposable
{
    private readonly MsSqlTestHelper _msSqlTestHelper;
    private readonly MsSqlOutbox _sqlOutbox;

    public When_Getting_Number_Of_Outstanding_Messages_From_The_Store()
    {
        _msSqlTestHelper = new MsSqlTestHelper();
        _msSqlTestHelper.SetupMessageDb();

        _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
    }

    [Fact]
    public async Task GivenAnOutbox_WhenOutstandingMessagesIsCalled_NumberOfOutstandingMessagesIsReturned()
    {
        var totalMessages = 123;
        var messagesToArchive = 62;
        for(int i = 0; i < totalMessages; i++)
        {
            await _sqlOutbox.AddAsync(GenerateMessage());
        }
       
        var outstadningMessages = await _sqlOutbox.GetNumberOfOutstandingMessagesAsync(CancellationToken.None);

        Assert.Equal(totalMessages, outstadningMessages);

        var messages = await _sqlOutbox.OutstandingMessagesAsync(0, messagesToArchive);

        await _sqlOutbox.MarkDispatchedAsync(messages.Select(m => m.Id).ToList());

        var outsandingMessagesPostArchival = await _sqlOutbox.GetNumberOfOutstandingMessagesAsync(CancellationToken.None);

        var expectedOutstanding = totalMessages - messagesToArchive;

        Assert.Equal(expectedOutstanding, outsandingMessagesPostArchival);
    }

    private Message GenerateMessage()
    {
        return new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
    }

    public void Dispose()
    {
        _msSqlTestHelper.CleanUpDb();
    }
}
