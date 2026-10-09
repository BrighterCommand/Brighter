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

using System.Threading;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Test-only gRPC fault-injecting interceptor (ADR 0078 "Evidence for the two failures a broker
/// cannot produce on demand"). Every call reaches the real emulator; when armed, only the result
/// of the named fully-qualified gRPC method(s) is replaced with an <see cref="RpcException"/>
/// carrying <see cref="StatusCode.FailedPrecondition"/> — never <see cref="StatusCode.Unavailable"/>,
/// which is the one retriable code for both Acknowledge and ModifyAckDeadline and would just be
/// retried rather than fail. Every other call, and every call while disarmed, passes through
/// untouched. This is not a mock (ADR 0078, "Why this is not a mock (C-10)").
/// </summary>
internal sealed class GcpFaultInjectingInterceptor(params string[] faultyMethods) : Interceptor
{
    private int _armed;
    private int _firedCount;

    /// <summary>
    /// The number of times the fault has fired. Guards against the injected invoker being silently
    /// dropped, and confirms at runtime that Validate accepts the invoker (ADR 0078 Risks).
    /// </summary>
    public int FiredCount => Volatile.Read(ref _firedCount);

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public void Disarm() => Interlocked.Exchange(ref _armed, 0);

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        MaybeFault(context.Method.FullName);
        return continuation(request, context);
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        MaybeFault(context.Method.FullName);
        return continuation(request, context);
    }

    private void MaybeFault(string methodFullName)
    {
        if (Volatile.Read(ref _armed) == 0)
        {
            return;
        }

        foreach (var faulty in faultyMethods)
        {
            if (methodFullName == faulty)
            {
                Interlocked.Increment(ref _firedCount);
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "test-injected fault"));
            }
        }
    }
}
