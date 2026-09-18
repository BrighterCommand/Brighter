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
/// A mapper whose single public constructor requires the <c>AddTransient</c>
/// <see cref="ITransientGateway"/>, which itself requires the <c>AddScoped</c> <see cref="IOrderDbContext"/> —
/// transitive captivity, which the direct-parameter-only check does not report (C-20(ii)).
/// </summary>
public sealed class TransitivelyCaptiveMapper : IAmAMessageMapper<TransitivelyCaptiveCommand>
{
    public TransitivelyCaptiveMapper(ITransientGateway gateway)
    {
    }

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(TransitivelyCaptiveCommand request, Publication publication) => throw new NotImplementedException();

    public TransitivelyCaptiveCommand MapToRequest(Message message) => throw new NotImplementedException();
}
