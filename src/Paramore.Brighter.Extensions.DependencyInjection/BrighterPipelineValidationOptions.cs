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

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for pipeline validation at startup.
/// Controls whether validation is owned by the consumer (ServiceActivator) or runs
/// independently via <see cref="BrighterValidationHostedService"/>.
/// </summary>
public class BrighterPipelineValidationOptions
{
    /// <summary>
    /// When true, validation is deferred to <c>ServiceActivatorHostedService</c> and the
    /// <see cref="BrighterValidationHostedService"/> becomes a no-op.
    /// Set automatically by <c>AddConsumers()</c>.
    /// </summary>
    public bool ConsumerOwnsValidation { get; set; }

    /// <summary>
    /// When true (the default), validation errors cause <see cref="PipelineValidationException"/> to
    /// be thrown, terminating host startup. When false, errors are logged at <c>LogLevel.Error</c>
    /// and the application continues starting.
    /// Set by <c>ValidatePipelines(throwOnError:)</c>.
    /// </summary>
    public bool ThrowOnError { get; set; } = true;
}
