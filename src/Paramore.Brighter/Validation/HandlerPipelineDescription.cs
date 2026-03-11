#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
/// Describes a handler pipeline for a given request type — the handler type,
/// whether it is sync or async, and the before/after attribute steps.
/// Produced by <see cref="PipelineBuilder{TRequest}.Describe(Type)"/> without
/// instantiating any handlers.
/// </summary>
public class HandlerPipelineDescription
{
    /// <summary>The request type this pipeline handles.</summary>
    public Type RequestType { get; }

    /// <summary>The concrete handler type.</summary>
    public Type HandlerType { get; }

    /// <summary>True if the handler extends <see cref="RequestHandlerAsync{TRequest}"/>.</summary>
    public bool IsAsync { get; }

    /// <summary>Attributes that run before the handler, sorted by step order.</summary>
    public IReadOnlyList<PipelineStepDescription> BeforeSteps { get; }

    /// <summary>Attributes that run after the handler, sorted by step order.</summary>
    public IReadOnlyList<PipelineStepDescription> AfterSteps { get; }

    /// <summary>
    /// Creates a new handler pipeline description.
    /// </summary>
    /// <param name="requestType">The request type.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="isAsync">Whether the handler is async.</param>
    /// <param name="beforeSteps">The before-handler steps.</param>
    /// <param name="afterSteps">The after-handler steps.</param>
    public HandlerPipelineDescription(
        Type requestType,
        Type handlerType,
        bool isAsync,
        IReadOnlyList<PipelineStepDescription> beforeSteps,
        IReadOnlyList<PipelineStepDescription> afterSteps)
    {
        RequestType = requestType;
        HandlerType = handlerType;
        IsAsync = isAsync;
        BeforeSteps = beforeSteps;
        AfterSteps = afterSteps;
    }
}
