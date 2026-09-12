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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A wrap transform whose constructor depends on <see cref="IUnregisteredDependency"/>, a type
/// deliberately never registered in a test's container, so resolving it always fails with the
/// container's own "unable to resolve service" exception — a transform resolution failure that occurs
/// <em>after</em> <see cref="PoisonedScopeMapper"/> has already been resolved successfully into the same
/// pipeline scope. Applied via <see cref="PoisonedWrapWith"/>.
/// </summary>
public sealed class PoisonedTransform : IAmAMessageTransform
{
    public PoisonedTransform(IUnregisteredDependency dependency)
    {
    }

    public IRequestContext? Context { get; set; }

    public void Dispose()
    {
    }

    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
    }

    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }

    public Message Wrap(Message message, Publication publication) => message;

    public Message Unwrap(Message message) => message;
}
