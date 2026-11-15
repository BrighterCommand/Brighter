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
    [Test]
    [Arguments(StatusCode.NotFound)]
    [Arguments(StatusCode.InvalidArgument)]
    [Arguments(StatusCode.DeadlineExceeded)]
    [Arguments(StatusCode.ResourceExhausted)]
    public async Task When_rpc_exception_has_non_tolerated_status_code_should_rethrow_unchanged(StatusCode code)
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var original = new RpcException(new Status(code, "Test failure"));

        // Act
        var thrown = await Assert.That(() => sut.TryCallAsync<string>(s_step, () => Task.FromException<string>(original))).ThrowsExactly<RpcException>();

        // Assert
        await Assert.That(thrown).IsSameReferenceAs(original);
        await Assert.That(System.Linq.Enumerable.Any(logger.Entries, e => e.Level == LogLevel.Warning)).IsFalse();
    }

    // ── Tolerated cases ───────────────────────────────────────────────────────

    /// <summary>
    /// When an RpcException carries a tolerated status code, TryCallAsync returns (false, default)
    /// and logs exactly one Warning carrying all five elements.
    /// </summary>
    [Test]
    [Arguments(StatusCode.Unimplemented)]
    [Arguments(StatusCode.PermissionDenied)]
    [Arguments(StatusCode.Unauthenticated)]
    public async Task When_rpc_exception_has_tolerated_status_code_should_return_false_and_log_one_warning(StatusCode code)
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var exc = new RpcException(new Status(code, "Tolerated failure"));

        // Act
        var (completed, result) = await sut.TryCallAsync<string>(s_step, () => Task.FromException<string>(exc));

        // Assert
        await Assert.That(completed).IsFalse();
        await Assert.That(result).IsNull();

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings).HasSingleItem();

        var message = warnings[0].Message;
        // Five elements: Helper, Rpc, Resource, Status, consequence
        await Assert.That(message).Contains(s_step.Helper);
        await Assert.That(message).Contains(s_step.Rpc);
        await Assert.That(message).Contains(s_step.Resource);
        await Assert.That(message).Contains(code.ToString());
        await Assert.That(message).Contains("native dead-lettering may be inactive");
    }

    // ── Client-construction case ──────────────────────────────────────────────

    /// <summary>
    /// When the factory supplied to TryCreateProjectsClientAsync throws InvalidOperationException,
    /// the method returns null and logs exactly one Warning. The Warning names "construct ProjectsClient"
    /// as the Rpc element and "InvalidOperationException" as the Status element.
    /// </summary>
    [Test]
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
        await Assert.That(result).IsNull();

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings).HasSingleItem();

        var message = warnings[0].Message;
        // Five elements: Helper, Rpc="construct ProjectsClient", Resource, Status="InvalidOperationException", consequence
        await Assert.That(message).Contains(constructionStep.Helper);
        await Assert.That(message).Contains("construct ProjectsClient");
        await Assert.That(message).Contains(constructionStep.Resource);
        await Assert.That(message).Contains("InvalidOperationException");
        await Assert.That(message).Contains("native dead-lettering may be inactive");
    }

    /// <summary>
    /// When the factory supplied to TryCreateProjectsClientAsync throws any type other than
    /// InvalidOperationException, the exception propagates unchanged.
    /// </summary>
    [Test]
    public async Task When_projects_client_factory_throws_other_exception_type_should_propagate_unchanged()
    {
        // Arrange
        var logger = new CapturingLogger<GcpIamCallTolerance>();
        var sut = new GcpIamCallTolerance(logger);
        var constructionStep = new IamStep("TestHelper", "construct ProjectsClient", "projects/test-project");
        var original = new ArgumentException("Unexpected construction failure");

        // Act
        var thrown = await Assert.That(() => sut.TryCreateProjectsClientAsync(
                constructionStep,
                () => Task.FromException<Google.Cloud.ResourceManager.V3.ProjectsClient>(original))).ThrowsExactly<ArgumentException>();

        // Assert
        await Assert.That(thrown).IsSameReferenceAs(original);
    }
}
