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
/// Describes one step in a handler pipeline — the attribute that declared it,
/// the handler type it resolves to, its step number, and its timing (before/after).
/// </summary>
/// <param name="AttributeType">The concrete <see cref="RequestHandlerAttribute"/> type (e.g. <c>typeof(UseInboxAttribute)</c>).</param>
/// <param name="HandlerType">The handler type returned by <see cref="RequestHandlerAttribute.GetHandlerType"/>.</param>
/// <param name="Step">The step number from the attribute.</param>
/// <param name="Timing">Whether this step runs before or after the main handler.</param>
public record PipelineStepDescription(Type AttributeType, Type HandlerType, int Step, HandlerTiming Timing);
