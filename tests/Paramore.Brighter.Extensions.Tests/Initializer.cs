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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Fixes <see cref="ApplicationLogging.LoggerFactory"/> to one stable instance before any test runs, and
/// forces the static <c>ILogger</c> fields of <see cref="TransformPipelineBuilder"/>,
/// <see cref="TransformPipelineBuilderAsync"/>, the internal <c>TransformPipelineDrain</c>, the internal
/// <c>ServiceProviderLifetimeScope</c> and <see cref="HandlerLifetimeScope"/> to bind to it.
/// </summary>
/// <remarks>
/// Without this, a test that adds a capturing <see cref="ILoggerProvider"/> to
/// <see cref="ApplicationLogging.LoggerFactory"/> is exposed to a real race: those types bind their
/// static logger to whichever factory instance <see cref="ApplicationLogging.LoggerFactory"/> holds the
/// <em>first time each type is touched in this process</em>, and never rebind. Some tests in this
/// assembly (e.g. one that resolves a <c>Dispatcher</c> from a <c>Host.CreateDefaultBuilder()</c>
/// container) trigger production code that reassigns <see cref="ApplicationLogging.LoggerFactory"/> to a
/// different instance — see <c>ServiceCollectionExtensions.BuildCommandProcessor</c> and
/// <c>ServiceActivator</c>'s <c>BuildDispatcher</c>, both of which adopt a DI-resolved
/// <c>ILoggerFactory</c> when one is registered. Under xUnit's parallel test execution the order between
/// that swap and a capturing test's own <c>AddProvider</c> call is not deterministic, so a provider added
/// to the then-current property value can silently miss every log call a type made before, or makes
/// after, whichever instance it actually bound to.
/// <para>
/// A <see cref="ModuleInitializerAttribute"/> runs before any test method in this assembly, so it wins
/// that race unconditionally: every listed type's static logger binds to <see cref="Factory"/> here,
/// before anything else in the process could have touched them or reassigned the property. A capturing
/// test should add its provider to <see cref="Factory"/> directly — not by reading
/// <see cref="ApplicationLogging.LoggerFactory"/>, which a later test may have since reassigned.
/// </para>
/// </remarks>
internal static class Initializer
{
    /// <summary>
    /// The one <see cref="ILoggerFactory"/> instance every type listed above is bound to for this
    /// process's whole test run. Add a capturing <see cref="ILoggerProvider"/> here directly.
    /// </summary>
    public static readonly ILoggerFactory Factory = new LoggerFactory();

    [ModuleInitializer]
    public static void Initialize()
    {
        ApplicationLogging.LoggerFactory = Factory;

        RuntimeHelpers.RunClassConstructor(typeof(TransformPipelineBuilder).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(TransformPipelineBuilderAsync).TypeHandle);

        var core = typeof(TransformPipelineBuilder).Assembly;
        RuntimeHelpers.RunClassConstructor(core.GetType("Paramore.Brighter.TransformPipelineDrain")!.TypeHandle);

        var diPackage = typeof(ServiceProviderMapperFactory).Assembly;
        RuntimeHelpers.RunClassConstructor(
            diPackage.GetType("Paramore.Brighter.Extensions.DependencyInjection.ServiceProviderLifetimeScope")!.TypeHandle);

        RuntimeHelpers.RunClassConstructor(typeof(HandlerLifetimeScope).TypeHandle);
    }
}
