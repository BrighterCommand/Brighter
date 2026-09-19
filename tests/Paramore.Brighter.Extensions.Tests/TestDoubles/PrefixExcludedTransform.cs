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

using System;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A transform defined in <c>Paramore.Brighter.Extensions.Tests</c>, requiring the <c>AddScoped</c>
/// <see cref="IOrderDbContext"/> — pins the assembly-name <b>prefix</b> half of FR-22.3's exclusion: this
/// assembly is not literally named <c>Paramore.Brighter</c>, only prefixed by it.
/// </summary>
public sealed class PrefixExcludedTransform : IAmAMessageTransform
{
    public PrefixExcludedTransform(IOrderDbContext context)
    {
    }

    public IRequestContext? Context { get; set; }

    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
    }

    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }

    public Message Wrap(Message message, Publication publication) => throw new NotImplementedException();

    public Message Unwrap(Message message) => throw new NotImplementedException();

    public void Dispose()
    {
    }
}
