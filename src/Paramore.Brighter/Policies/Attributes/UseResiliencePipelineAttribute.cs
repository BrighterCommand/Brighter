#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Policies.Attributes;

/// <summary>
/// This attribute supports the use of <a href="https://github.com/App-vNext/Polly">Polly</a> to provide quality of service
/// around exceptions thrown from subsequent steps in the Brighter handler pipeline.
/// A Polly <see cref="ResiliencePipeline"/> can be used to support a Retry, Circuit Breaker, Timeout,
/// or other resilience strategy approach to exception handling for the decorated handler method.
/// </summary>
/// <remarks>
/// <para>
/// Policies used by this attribute are identified by a string-based key, which is used as a lookup
/// into an <see cref="ResiliencePipelineRegistry{TKey}"/>. It is assumed that you have registered
/// your required resilience pipelines with a <see cref="ResiliencePipelineRegistry{TKey}"/> instance
/// and configured that registry as a dependency of the <see cref="Paramore.Brighter.CommandProcessor"/>
/// (or similar Brighter dispatcher) using the <see cref="Paramore.Brighter.CommandProcessorBuilder"/>.
/// </para>
/// <para>
/// When applied, Brighter will automatically wrap the execution of the decorated handler method
/// with the specified <see cref="ResiliencePipeline"/>, ensuring that operations
/// are resilient to transient faults.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class UseResiliencePipelineAttribute : RequestHandlerAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UseResiliencePipelineAttribute"/> class.
    /// </summary>
    /// <param name="policy">
    /// The key identifying the <see cref="ResiliencePipeline"/> to use.
    /// This key is used as a lookup into the <see cref="ResiliencePipelineRegistry{TKey}"/>
    /// that is configured with the <see cref="Paramore.Brighter.CommandProcessor"/>.
    /// </param>
    /// <param name="step">
    /// The zero-based order in which this attribute should be applied within the handler pipeline.
    /// Lower values are executed earlier. This attribute is typically applied <see cref="Paramore.Brighter.HandlerTiming.Before"/>
    /// subsequent handler logic.
    /// </param>
    public UseResiliencePipelineAttribute(string policy, int step) : base(step, HandlerTiming.Before)
    {
        Policy = policy;
    }
    
    /// <summary>
    /// Gets the key that identifies the <see cref="ResiliencePipeline"/> to be applied.
    /// </summary>
    /// <value>
    /// The string key used to retrieve the desired <see cref="ResiliencePipeline"/> from the registry.
    /// </value>
    public string Policy { get; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the pipeline should be scoped per handler type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, the <see cref="ResiliencePipeline"/> is looked up using a key
    /// composed of the handler type's full name and the specified <see cref="Policy"/> name.
    /// This allows different instances of the same resilience strategy (e.g., a circuit breaker)
    /// to be associated uniquely with each specific handler type.
    /// </para>
    /// <para>
    /// When <see langword="false"/> (default), the pipeline is looked up solely by the <see cref="Policy"/> name,
    /// implying a shared pipeline instance across all handlers using that same policy name.
    /// </para>
    /// <para>
    /// This is particularly useful for strategies like Circuit Breaker, where you might want a separate
    /// breaker for each distinct handler's external dependency.
    /// </para>
    /// </remarks>
    public bool UseTypePipeline { get; set; }

    /// <inheritdoc />
    public override object[] InitializerParams()
    {
        return [Policy, UseTypePipeline];
    }

    /// <inheritdoc />
    public override Type GetHandlerType()
    {
        return typeof(ResilienceExceptionPolicyHandler<>);
    }
}
