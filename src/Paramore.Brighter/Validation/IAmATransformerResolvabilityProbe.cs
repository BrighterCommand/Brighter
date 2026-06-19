#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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
/// Knows whether a message transformer type can be resolved, without instantiating it and without
/// building a transform pipeline. Pipeline validation uses this to detect a transform declared on a
/// mapper whose transformer was never registered — a signal that its assembly was not scanned — while
/// honouring the constraint that validation must not create transformers or build the (un-buildable)
/// pipeline. The default implementation answers from registration membership; applications may register
/// their own implementation if they resolve transformers through a non-standard mechanism.
/// </summary>
public interface IAmATransformerResolvabilityProbe
{
    /// <summary>
    /// Returns true when <paramref name="transformerType"/> is registered/resolvable. Never instantiates
    /// the transformer and never throws for an unregistered type (returns false).
    /// </summary>
    /// <param name="transformerType">The transformer type, as returned by a transform attribute's
    /// <see cref="TransformAttribute.GetHandlerType"/>.</param>
    /// <returns>True if the transformer type is resolvable; otherwise false.</returns>
    bool Resolves(Type transformerType);
}
