#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A sync handler for <see cref="TransientHandlerSendCommand"/> (AC-47) whose own pipeline is
/// <c>Transient</c> and therefore takes no pipeline scope of its own; issues a nested <c>Post</c> of
/// <see cref="TransientHandlerSendPostedCommand"/> from inside <c>Handle</c>, before the ambient scope
/// has been suppressed by anything (a plain <c>Send</c>, not a <c>Publish</c> subscriber).
/// </summary>
public sealed class TransientHandlerSendCommandHandler : RequestHandler<TransientHandlerSendCommand>
{
    private readonly IAmACommandProcessor _commandProcessor;

    public TransientHandlerSendCommandHandler(IAmACommandProcessor commandProcessor)
    {
        _commandProcessor = commandProcessor;
    }

    public override TransientHandlerSendCommand Handle(TransientHandlerSendCommand command)
    {
        _commandProcessor.Post(new TransientHandlerSendPostedCommand());
        return base.Handle(command);
    }
}
