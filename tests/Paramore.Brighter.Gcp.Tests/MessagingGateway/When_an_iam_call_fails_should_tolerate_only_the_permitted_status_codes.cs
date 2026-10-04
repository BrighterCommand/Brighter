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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// AC-21 (R-20, NFR-5): The IAM status-code filter tolerates only Unimplemented, PermissionDenied
/// and Unauthenticated; everything else is rethrown. Client construction tolerates only
/// <see cref="InvalidOperationException"/>. Exercised directly without any broker or mock transport.
/// </summary>
public class GcpIamCallToleranceTests
{
    private static readonly IamStep s_step = new("TestHelper", "GetProjectAsync", "projects/test-project");

    // ── Rethrown-unchanged cases ──────────────────────────────────────────────

    /// <summary>
    /// When an RpcException carries a status code outside the tolerated set, it is rethrown as the
    /// same exception instance and no Warning is logged.
    /// </summary>
    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    public async Task When_rpc_exception_has_non_tolerated_status_code_should_rethrow_unchanged(StatusCode code)
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var original = new RpcException(new Status(code, "Test failure"));

        // Act
        var thrown = await Assert.ThrowsAsync<RpcException>(
            () => sut.TryCallAsync<string>(s_step, () => Task.FromException<string>(original)));

        // Assert
        Assert.Same(original, thrown);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ── Tolerated cases ───────────────────────────────────────────────────────

    /// <summary>
    /// When an RpcException carries a tolerated status code, TryCallAsync returns (false, default)
    /// and logs exactly one Warning carrying all five elements.
    /// </summary>
    [Theory]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task When_rpc_exception_has_tolerated_status_code_should_return_false_and_log_one_warning(StatusCode code)
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var exc = new RpcException(new Status(code, "Tolerated failure"));

        // Act
        var (completed, result) = await sut.TryCallAsync<string>(s_step, () => Task.FromException<string>(exc));

        // Assert
        Assert.False(completed);
        Assert.Null(result);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);

        var message = warnings[0].Message;
        // Five elements: Helper, Rpc, Resource, Status, consequence
        Assert.Contains(s_step.Helper, message);
        Assert.Contains(s_step.Rpc, message);
        Assert.Contains(s_step.Resource, message);
        Assert.Contains(code.ToString(), message);
        Assert.Contains("native dead-lettering may be inactive", message);
    }

    // ── Client-construction case ──────────────────────────────────────────────

    /// <summary>
    /// When the factory supplied to TryCreateProjectsClientAsync throws InvalidOperationException,
    /// the method returns null and logs exactly one Warning. The Warning names "construct ProjectsClient"
    /// as the Rpc element and "InvalidOperationException" as the Status element.
    /// </summary>
    [Fact]
    public async Task When_projects_client_factory_throws_invalid_operation_exception_should_return_null_and_log_warning()
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var constructionStep = new IamStep("TestHelper", "construct ProjectsClient", "projects/test-project");
        var exc = new InvalidOperationException("Your default credentials were not found.");

        // Act
        var result = await sut.TryCreateProjectsClientAsync(
            constructionStep,
            () => Task.FromException<Google.Cloud.ResourceManager.V3.ProjectsClient>(exc));

        // Assert
        Assert.Null(result);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);

        var message = warnings[0].Message;
        // Five elements: Helper, Rpc="construct ProjectsClient", Resource, Status="InvalidOperationException", consequence
        Assert.Contains(constructionStep.Helper, message);
        Assert.Contains("construct ProjectsClient", message);
        Assert.Contains(constructionStep.Resource, message);
        Assert.Contains("InvalidOperationException", message);
        Assert.Contains("native dead-lettering may be inactive", message);
    }

    /// <summary>
    /// When the factory supplied to TryCreateProjectsClientAsync throws any type other than
    /// InvalidOperationException, the exception propagates unchanged.
    /// </summary>
    [Fact]
    public async Task When_projects_client_factory_throws_other_exception_type_should_propagate_unchanged()
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var constructionStep = new IamStep("TestHelper", "construct ProjectsClient", "projects/test-project");
        var original = new ArgumentException("Unexpected construction failure");

        // Act
        var thrown = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.TryCreateProjectsClientAsync(
                constructionStep,
                () => Task.FromException<Google.Cloud.ResourceManager.V3.ProjectsClient>(original)));

        // Assert
        Assert.Same(original, thrown);
    }
}
