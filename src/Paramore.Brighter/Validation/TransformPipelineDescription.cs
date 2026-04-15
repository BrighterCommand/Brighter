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
using System.Collections.Generic;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Describes a transform pipeline for a given request type — the mapper type,
/// whether it is a default mapper, and the wrap/unwrap transform steps.
/// Produced by <see cref="TransformPipelineBuilder.DescribeTransforms"/> without
/// instantiating any mappers or transforms.
/// </summary>
/// <param name="mapperType">The resolved mapper type for this request type.</param>
/// <param name="isDefaultMapper">True if the mapper was resolved from a default mapper template.</param>
/// <param name="wrapTransforms">Outgoing transform steps (wrap), sorted by step order.</param>
/// <param name="unwrapTransforms">Incoming transform steps (unwrap), sorted by step order.</param>
public sealed class TransformPipelineDescription(
    Type mapperType,
    bool isDefaultMapper,
    IReadOnlyList<TransformStepDescription> wrapTransforms,
    IReadOnlyList<TransformStepDescription> unwrapTransforms)
{
    /// <summary>The resolved mapper type for this request type.</summary>
    public Type MapperType { get; } = mapperType;

    /// <summary>True if the mapper was resolved from a default mapper template.</summary>
    public bool IsDefaultMapper { get; } = isDefaultMapper;

    /// <summary>Outgoing transform steps (wrap), sorted by step order.</summary>
    public IReadOnlyList<TransformStepDescription> WrapTransforms { get; } = wrapTransforms;

    /// <summary>Incoming transform steps (unwrap), sorted by step order.</summary>
    public IReadOnlyList<TransformStepDescription> UnwrapTransforms { get; } = unwrapTransforms;
}
