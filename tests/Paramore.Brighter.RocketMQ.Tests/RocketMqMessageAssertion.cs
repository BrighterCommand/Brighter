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

using Paramore.Brighter;

namespace Paramore.Brighter.RocketMQ.Tests;

/// <summary>
/// RocketMQ-specific implementation of <see cref="IAmAMessageAssertion"/> that handles requeued messages
/// where the MessageId changes and the original ID is stored in the message bag.
/// </summary>
public class RocketMqMessageAssertion : IAmAMessageAssertion
{
    /// <inheritdoc />
    public void Assert(Message expected, Message actual)
    {
       Xunit.Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
       Xunit.Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
       Xunit.Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
       Xunit.Assert.Equal(expected.Header.DataSchema, actual.Header.DataSchema);

       // Requeued messages get a new MessageId; the original is stored in the bag
       if (actual.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName))
       {
           Xunit.Assert.Equal(expected.Header.MessageId.ToString(),
               actual.Header.Bag[Message.OriginalMessageIdHeaderName]?.ToString());
       }
       else
       {
           Xunit.Assert.Equal(expected.Header.MessageId, actual.Header.MessageId);
       }

       Xunit.Assert.Equal(expected.Header.PartitionKey, actual.Header.PartitionKey);
       Xunit.Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
       Xunit.Assert.Equal(expected.Header.Subject, actual.Header.Subject);
       Xunit.Assert.Equal(expected.Header.SpecVersion, actual.Header.SpecVersion);
       Xunit.Assert.Equal(expected.Header.Source, actual.Header.Source);
       Xunit.Assert.Equal(expected.Header.Topic, actual.Header.Topic);
       Xunit.Assert.Equal(expected.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), actual.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
       Xunit.Assert.Equal(expected.Header.Type, actual.Header.Type);
       Xunit.Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
       Xunit.Assert.Equal(System.TimeSpan.Zero, actual.Header.Delayed);
       Xunit.Assert.Equal(expected.Body.Value, actual.Body.Value);
       Xunit.Assert.Equal(expected.Header.TraceParent, actual.Header.TraceParent);
       Xunit.Assert.Equal(expected.Header.TraceState, actual.Header.TraceState);
       Xunit.Assert.Equal(expected.Header.Baggage, actual.Header.Baggage);
    }
}
