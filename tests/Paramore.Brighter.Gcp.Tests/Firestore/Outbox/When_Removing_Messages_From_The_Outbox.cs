#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using Paramore.Brighter.Outbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Outbox;

[Trait("Category", "Firestore")]
public class OutboxDeletingMessagesTests
{
    private readonly Message _firstMessage = new(new MessageHeader(Id.Random, new RoutingKey("Test"),
            MessageType.MT_COMMAND,
            timeStamp: DateTime.UtcNow.AddHours(-3)), new MessageBody("Body")
    );
    private readonly Message _secondMessage = new(new MessageHeader(Id.Random, new RoutingKey("Test2"),
            MessageType.MT_COMMAND,
            timeStamp: DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2")
    );
    private readonly Message _thirdMessage = new(new MessageHeader(Id.Random, new RoutingKey("Test3"),
            MessageType.MT_COMMAND,
            timeStamp: DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3")
    );
    private readonly FirestoreOutbox _outbox = new(Configuration.CreateOutbox());

    [Fact]
    public void When_Removing_Messages_From_The_Outbox()
    {
        //arrange
        var context = new RequestContext();

        //act
        _outbox.Add(_firstMessage, context);
        _outbox.Add(_secondMessage, context);
        _outbox.Add(_thirdMessage, context);

        _outbox.Delete([_firstMessage.Id], context);

        //assert
        var remainingMessages = _outbox.OutstandingMessages(TimeSpan.Zero, context);

        var msgs = remainingMessages as Message[] ?? remainingMessages.ToArray();
        Assert.True(msgs.Length > 2);
        Assert.Contains(_secondMessage, msgs);
        Assert.Contains(_thirdMessage, msgs);

        _outbox.Delete([_secondMessage.Id, _thirdMessage.Id], context);

        var messages = _outbox.OutstandingMessages(TimeSpan.Zero, context);

        messages = messages.ToList();
        Assert.DoesNotContain(messages, x => x.Id == _firstMessage.Id);
        Assert.DoesNotContain(messages, x => x.Id == _secondMessage.Id);
        Assert.DoesNotContain(messages, x => x.Id == _thirdMessage.Id);
    }
}
