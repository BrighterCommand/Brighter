#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class GcpPurgeFaultInterceptor : Interceptor
{
    public Action<string, object>? BeforeCall { get; set; }
    public Action<string, object>? AfterCall { get; set; }
    public ConcurrentDictionary<string, byte> AttemptedTopics { get; } = new();
    public ConcurrentDictionary<string, byte> AttemptedSubscriptions { get; } = new();

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ObserveRequest(context.Method.Name, request);
        var response = continuation(request, context);
        AfterCall?.Invoke(context.Method.Name, request);
        return response;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ObserveRequest(context.Method.Name, request);
        var call = continuation(request, context);
        return new AsyncUnaryCall<TResponse>(ObserveResponseAsync(call.ResponseAsync, context.Method.Name, request),
            call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
    }

    private async Task<TResponse> ObserveResponseAsync<TResponse>(Task<TResponse> response, string method, object request)
    {
        var result = await response;
        AfterCall?.Invoke(method, request);
        return result;
    }

    private void ObserveRequest(string method, object request)
    {
        if (request is Topic topic)
            AttemptedTopics.TryAdd(topic.Name, 0);
        if (request is Google.Cloud.PubSub.V1.Subscription subscription)
            AttemptedSubscriptions.TryAdd(subscription.Name, 0);
        BeforeCall?.Invoke(method, request);
    }
}
