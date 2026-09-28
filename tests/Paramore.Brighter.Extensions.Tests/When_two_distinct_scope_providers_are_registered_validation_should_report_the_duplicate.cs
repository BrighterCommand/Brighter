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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// AC-32 (FR-24.3) — two distinct <see cref="IAmAScopeProvider"/> registrations produce exactly one
/// warning naming both, identifying the last-registered one as effective; a repeated registration of the
/// same implementation type is idempotent in effect and produces no finding.
/// </summary>
public class DuplicateScopeProviderValidationTests
{
    [Fact]
    public void When_two_distinct_scope_providers_are_registered_validation_should_report_the_duplicate()
    {
        // Arrange — two distinct IAmAScopeProvider implementations, each registered with a plain
        // AddSingleton in a stated order, and all three pipeline lifetimes Scoped
        var services = new ServiceCollection();
        services.AddSingleton<IAmAScopeProvider, AsyncLocalScopeProvider>();
        services.AddSingleton<IAmAScopeProvider, ThrowingScopeProvider>();
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — exactly one warning naming both provider types, identifying the last-registered one
        // (ThrowingScopeProvider) as effective, and pointing at the guidance page
        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(nameof(AsyncLocalScopeProvider), warning.Message);
        Assert.Contains(nameof(ThrowingScopeProvider), warning.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", warning.Message);

        // Assert — MS DI itself resolves the last-registered provider for the unkeyed service type, so the
        // provider the warning names as effective is the one the container-backed factories will actually ask
        var resolved = provider.GetRequiredService<IAmAScopeProvider>();
        Assert.IsType<ThrowingScopeProvider>(resolved);
    }

    [Fact]
    public void When_the_same_scope_provider_type_is_registered_twice_validation_should_not_report_a_duplicate()
    {
        // Arrange — the same implementation type registered twice, all three pipeline lifetimes Scoped
        var services = new ServiceCollection();
        services.AddSingleton<IAmAScopeProvider, AsyncLocalScopeProvider>();
        services.AddSingleton<IAmAScopeProvider, AsyncLocalScopeProvider>();
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — a repeated registration of the same type is idempotent in effect: no finding at all
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }
}
