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
using System.Threading.Tasks;
using Google.Cloud.ResourceManager.V3;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Records the context of one IAM helper step for use in Warning diagnostics.
/// </summary>
/// <param name="Helper">The name of the IAM helper method being executed.</param>
/// <param name="Rpc">The name of the RPC or operation being attempted.</param>
/// <param name="Resource">The GCP resource path being operated on.</param>
public sealed record IamStep(string Helper, string Rpc, string Resource);

/// <summary>
/// Runs individual IAM helper steps and tolerates the GCP status codes that indicate the
/// calling principal lacks project-level IAM permissions. Tolerated failures are logged at
/// Warning and the helper is abandoned; all other failures propagate unchanged.
/// </summary>
/// <remarks>
/// The tolerated set is <c>Unimplemented</c>, <c>PermissionDenied</c> and <c>Unauthenticated</c>
/// (R-20, NFR-5, ADR 0078). This tolerance is scoped to IAM helper calls only; it does not
/// extend to topic creation, subscription creation, or any call on the message path.
/// </remarks>
public sealed partial class GcpIamCallTolerance
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="GcpIamCallTolerance"/>.
    /// </summary>
    /// <param name="logger">
    /// Optional logger. When <c>null</c>, the ambient <see cref="ApplicationLogging"/> logger is used.
    /// </param>
    public GcpIamCallTolerance(ILogger? logger = null)
    {
        _logger = logger ?? ApplicationLogging.CreateLogger<GcpIamCallTolerance>();
    }

    /// <summary>
    /// Runs one IAM RPC. Returns <c>(true, result)</c> on success or <c>(false, default)</c>
    /// when the exception's status code is in the tolerated set, after logging one Warning.
    /// Any <see cref="RpcException"/> outside the tolerated set, and any other exception, propagates unchanged.
    /// </summary>
    /// <typeparam name="T">The type returned by the RPC.</typeparam>
    /// <param name="step">Diagnostic context for the Warning message.</param>
    /// <param name="rpc">The IAM RPC to invoke.</param>
    /// <returns>
    /// <c>(true, result)</c> on success; <c>(false, default)</c> on a tolerated failure.
    /// </returns>
    public async Task<(bool completed, T? result)> TryCallAsync<T>(IamStep step, Func<Task<T>> rpc)
    {
        try
        {
            var result = await rpc();
            return (true, result);
        }
        catch (RpcException ex) when (IsTolerated(ex.StatusCode))
        {
            Log.IamCallAbandoned(_logger, step.Helper, step.Rpc, step.Resource, ex.StatusCode.ToString());
            return (false, default);
        }
    }

    /// <summary>
    /// Attempts to construct a <see cref="ProjectsClient"/>. Returns <c>null</c> when the factory
    /// throws <see cref="InvalidOperationException"/> (credential resolution failure), after logging
    /// one Warning. Any other exception propagates unchanged.
    /// </summary>
    /// <param name="step">Diagnostic context for the Warning message.</param>
    /// <param name="create">Factory that builds the <see cref="ProjectsClient"/>.</param>
    /// <returns>The constructed client, or <c>null</c> on a tolerated construction failure.</returns>
    public async Task<ProjectsClient?> TryCreateProjectsClientAsync(IamStep step, Func<Task<ProjectsClient>> create)
    {
        try
        {
            return await create();
        }
        catch (InvalidOperationException)
        {
            Log.IamCallAbandoned(_logger, step.Helper, step.Rpc, step.Resource, nameof(InvalidOperationException));
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the status code is in the tolerated set:
    /// <see cref="StatusCode.Unimplemented"/>, <see cref="StatusCode.PermissionDenied"/>, or
    /// <see cref="StatusCode.Unauthenticated"/>.
    /// </summary>
    /// <param name="code">The gRPC status code to test.</param>
    /// <returns><c>true</c> if the code should be tolerated; otherwise <c>false</c>.</returns>
    public static bool IsTolerated(StatusCode code) =>
        code is StatusCode.Unimplemented or StatusCode.PermissionDenied or StatusCode.Unauthenticated;

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning,
            "{Helper} abandoned: {Rpc} on {Resource} failed with {Status}; native dead-lettering may be inactive")]
        public static partial void IamCallAbandoned(ILogger logger, string helper, string rpc, string resource, string status);
    }
}
