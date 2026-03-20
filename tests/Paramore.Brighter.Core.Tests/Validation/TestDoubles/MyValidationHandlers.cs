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

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Policies.Attributes;
using Paramore.Brighter.Reject.Attributes;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>A public sync handler for validation rule tests.</summary>
public class MyPublicSyncHandler : RequestHandler<MyDescribableCommand>
{
    public override MyDescribableCommand Handle(MyDescribableCommand command) => base.Handle(command);
}

/// <summary>A public async handler for validation rule tests.</summary>
public class MyPublicAsyncHandler : RequestHandlerAsync<MyDescribableCommand>
{
    public override Task<MyDescribableCommand> HandleAsync(MyDescribableCommand command, CancellationToken cancellationToken = default)
        => base.HandleAsync(command, cancellationToken);
}

/// <summary>An internal (non-public) handler type — used to test HandlerTypeVisibility rule.</summary>
internal class MyInternalHandler : RequestHandler<MyDescribableCommand>
{
    public override MyDescribableCommand Handle(MyDescribableCommand command) => base.Handle(command);
}

/// <summary>A type that is neither a sync nor async handler — used to test AttributeAsyncConsistency rule.</summary>
public class MyNonHandlerType { }

/// <summary>
/// A public handler with misordered backstop (step 5) and resilience (step 3) attributes.
/// In Brighter, lower step = outer wrapper, so the backstop at step 5 is inner and will
/// never catch resilience failures — triggers a BackstopAttributeOrdering warning.
/// </summary>
public class MyMisorderedBackstopHandler : RequestHandler<MyDescribableCommand>
{
    [RejectMessageOnError(5)]
    [UseResiliencePipeline("test-policy", step: 3)]
    public override MyDescribableCommand Handle(MyDescribableCommand command) => base.Handle(command);
}
