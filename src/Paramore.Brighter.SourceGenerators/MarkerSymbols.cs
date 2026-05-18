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

using Microsoft.CodeAnalysis;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Cached lookup of the Brighter framework type symbols the reader needs to recognise.
/// Returns <see cref="IsValid"/> = false when Brighter isn't in the compilation's reference graph.
/// </summary>
public sealed class MarkerSymbols
{
    public INamedTypeSymbol? BrighterBuilder { get; private set; }
    public INamedTypeSymbol? HandleRequests { get; private set; }
    public INamedTypeSymbol? HandleRequestsAsync { get; private set; }
    public INamedTypeSymbol? MessageMapper { get; private set; }
    public INamedTypeSymbol? MessageMapperAsync { get; private set; }
    public INamedTypeSymbol? MessageTransform { get; private set; }
    public INamedTypeSymbol? MessageTransformAsync { get; private set; }

    public bool IsValid =>
        BrighterBuilder is not null &&
        HandleRequests is not null &&
        HandleRequestsAsync is not null &&
        MessageMapper is not null &&
        MessageMapperAsync is not null &&
        MessageTransform is not null &&
        MessageTransformAsync is not null;

    public static MarkerSymbols Resolve(Compilation c) => new()
    {
        BrighterBuilder = c.GetTypeByMetadataName("Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder"),
        HandleRequests = c.GetTypeByMetadataName("Paramore.Brighter.IHandleRequests`1"),
        HandleRequestsAsync = c.GetTypeByMetadataName("Paramore.Brighter.IHandleRequestsAsync`1"),
        MessageMapper = c.GetTypeByMetadataName("Paramore.Brighter.IAmAMessageMapper`1"),
        MessageMapperAsync = c.GetTypeByMetadataName("Paramore.Brighter.IAmAMessageMapperAsync`1"),
        MessageTransform = c.GetTypeByMetadataName("Paramore.Brighter.IAmAMessageTransform"),
        MessageTransformAsync = c.GetTypeByMetadataName("Paramore.Brighter.IAmAMessageTransformAsync"),
    };
}
