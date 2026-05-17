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

namespace Paramore.Brighter.Validation;

/// <summary>
/// Describes one step in a transform pipeline — the attribute that declared it,
/// the transform type it resolves to, and its step number.
/// </summary>
/// <param name="attributeType">The concrete <see cref="TransformAttribute"/> type (e.g. a <see cref="WrapWithAttribute"/> subclass).</param>
/// <param name="transformType">The transform type returned by <see cref="TransformAttribute.GetHandlerType"/>.</param>
/// <param name="step">The step number from the attribute.</param>
public sealed class TransformStepDescription(Type attributeType, Type transformType, int step)
{
    /// <summary>The concrete attribute type that declared this step.</summary>
    public Type AttributeType { get; } = attributeType;

    /// <summary>The transform type from <see cref="TransformAttribute.GetHandlerType"/>.</summary>
    public Type TransformType { get; } = transformType;

    /// <summary>The step number from the attribute.</summary>
    public int Step { get; } = step;
}
