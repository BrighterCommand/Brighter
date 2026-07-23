#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Outbox.SpannerText;

// Regression for PR #4067 review: on Spanner the CausationId column always ships in the DDL, so the
// bulk deposit path always binds a per-row causation parameter. In the normal (non-replay) case the
// causation id is null. The bulk path names its parameters "@p{i}_CausationId", which did not match the
// exact-name ("@CausationId") special case in SpannerOutbox.CreateSqlParameter, so it produced an
// untyped DBNull that the emulator/production rejects. A bulk Add of >1 message therefore threw.
[Trait("Category", "Spanner")]
public class SpannerBulkAddNullCausationTests : IDisposable
{
    private readonly SpannerTextOutboxProvider _outboxProvider;
    private readonly IAmAMessageBuilder _messageBuilder;
    private readonly List<Message> _createdMessages = [];

    public SpannerBulkAddNullCausationTests()
    {
        _outboxProvider = new SpannerTextOutboxProvider();
        _outboxProvider.CreateStore();

        _messageBuilder = new DefaultMessageBuilder();
    }

    [Fact]
    public void When_bulk_adding_messages_with_null_causation_should_not_throw()
    {
        // Arrange
        var context = new RequestContext(); // no causation id in the bag -> causation binds as null
        var messages = new List<Message> { _messageBuilder.Build(), _messageBuilder.Build() };
        _createdMessages.AddRange(messages);

        var outbox = _outboxProvider.CreateOutbox();

        // Act
        var exception = Record.Exception(() => outbox.Add(messages, context));

        // Assert
        Assert.Null(exception);
        foreach (var message in messages)
        {
            var stored = outbox.Get(message.Id, context);
            Assert.Equal(message.Id, stored.Id);
        }
    }

    public void Dispose()
    {
        _outboxProvider.DeleteStore(_createdMessages);
    }
}
