using System;
using System.Collections.Generic;
using Paramore.Brighter.Sqlite.Tests.Outbox.Text;
using Paramore.Brighter.Sqlite.Tests.Outbox.Text.Sync;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

/// <summary>
/// CloudEvents defines <c>dataschema</c> as a URI-reference, which MAY be relative. Every Outbox
/// other than the relational one reads it back with <see cref="UriKind.RelativeOrAbsolute"/>; the
/// relational Outbox used <see cref="UriKind.Absolute"/> and so silently dropped a relative
/// dataschema to null on read — losing data rather than failing loudly.
/// </summary>
[Trait("Category", "Sqlite")]
public class RelationalOutboxRelativeDataSchemaTests : IDisposable
{
    private readonly IAmAnOutboxProviderSync _outboxProvider;
    private readonly List<Message> _createdMessages = [];

    public RelationalOutboxRelativeDataSchemaTests()
    {
        _outboxProvider = new SqliteTextOutboxProvider();
        _outboxProvider.CreateStore();
    }

    [Fact]
    public void When_storing_a_message_with_a_relative_dataschema_should_read_it_back()
    {
        // Arrange — the relative dataschema is the only data that decides this test
        var relativeDataSchema = new Uri("/schemas/v1", UriKind.Relative);

        var stored = new DefaultMessageBuilder()
            .SetDataSchema(relativeDataSchema)
            .Build();

        var context = new RequestContext();
        var outbox = _outboxProvider.CreateOutbox();
        _createdMessages.Add(stored);
        outbox.Add([stored], context);

        // Act
        var read = outbox.Get(stored.Id, context);

        // Assert
        Assert.NotNull(read);
        Assert.Equal(relativeDataSchema, read.Header.DataSchema);
    }

    public void Dispose() => _outboxProvider.DeleteStore(_createdMessages);
}
