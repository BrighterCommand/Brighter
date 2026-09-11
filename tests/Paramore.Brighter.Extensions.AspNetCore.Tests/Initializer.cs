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
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Fixes <see cref="ApplicationLogging.LoggerFactory"/> to one stable instance before any test runs, and
/// forces the static <c>ILogger</c> fields of every closed generic Brighter type this assembly's tests
/// touch (<see cref="PipelineBuilder{TRequest}"/>, <see cref="RequestHandler{TRequest}"/>,
/// <see cref="WrapPipeline{TRequest}"/>) plus <see cref="TransformPipelineBuilder"/>, to bind to it.
/// </summary>
/// <remarks>
/// Every <c>PlaceOrderWebApplicationFactory</c> a test constructs builds its own <c>CommandProcessor</c>
/// via <c>ServiceCollectionExtensions.BuildCommandProcessor</c>, which unconditionally reassigns
/// <see cref="ApplicationLogging.LoggerFactory"/> to that host's own DI-resolved <c>ILoggerFactory</c> -
/// and disposes that factory when the test's <c>await using var factory</c> disposes the host. A closed
/// generic Brighter type's static logger field is only ever initialised once per process, the first time
/// that exact closed type is touched, by calling <see cref="ApplicationLogging.CreateLogger{T}"/> against
/// whichever factory the property currently holds. Under xUnit's parallel test execution, if a brand new
/// request type's pipeline is first built while <see cref="ApplicationLogging.LoggerFactory"/> happens to
/// hold a different test's already-disposed host factory, that first touch throws
/// <see cref="System.ObjectDisposedException"/> - non-deterministically, only for whichever closed type
/// had not yet been touched by any earlier test. This surfaced when adding
/// <see cref="SharedMarkerSentCommand"/>: a brand new <c>Send</c> request type, whose
/// <c>PipelineBuilder&lt;SharedMarkerSentCommand&gt;</c> had never been touched before, could lose this
/// race roughly one run in three.
/// <para>
/// A <see cref="ModuleInitializerAttribute"/> runs before any test method in this assembly, so it wins
/// that race unconditionally: every closed type this assembly's tests use is warmed up against
/// <see cref="Factory"/> here, before anything else in the process could have touched it or reassigned
/// the property. Any future test that introduces a new request/command type sent or posted through a
/// <c>PlaceOrderWebApplicationFactory</c>-based host must add its own closed generics here too.
/// </para>
/// </remarks>
internal static class Initializer
{
    /// <summary>
    /// The one <see cref="ILoggerFactory"/> instance every type listed above is bound to for this
    /// process's whole test run.
    /// </summary>
    public static readonly ILoggerFactory Factory = new LoggerFactory();

    [ModuleInitializer]
    public static void Initialize()
    {
        ApplicationLogging.LoggerFactory = Factory;

        RuntimeHelpers.RunClassConstructor(typeof(TransformPipelineBuilder).TypeHandle);

        RuntimeHelpers.RunClassConstructor(typeof(PipelineBuilder<PlaceOrder>).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(PipelineBuilder<SharedMarkerSentCommand>).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(PipelineBuilder<NoHttpContextCommand>).TypeHandle);

        RuntimeHelpers.RunClassConstructor(typeof(RequestHandler<PlaceOrder>).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RequestHandler<SharedMarkerSentCommand>).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(RequestHandler<NoHttpContextCommand>).TypeHandle);

        RuntimeHelpers.RunClassConstructor(typeof(WrapPipeline<PostedOrderCommand>).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(WrapPipeline<SharedMarkerPostedCommand>).TypeHandle);
    }
}
