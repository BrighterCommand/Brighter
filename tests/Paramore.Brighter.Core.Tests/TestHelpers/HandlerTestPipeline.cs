#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

namespace Paramore.Brighter.Core.Tests.TestHelpers;

// Direct handler tests still need the logger configuration normally supplied by dispatch.
internal static class HandlerTestPipeline
{
    public static PipelineBuilder<TRequest> Create<TRequest>(RequestHandler<TRequest> handler, TRequest request)
        where TRequest : class, IRequest
    {
        var registry = new SubscriberRegistry();
        registry.Add(typeof(TRequest), handler.GetType());
        var builder = new PipelineBuilder<TRequest>(registry,
            new SimpleHandlerFactorySync(_ => handler), Initializer.TestLoggerFactory);
        builder.Build(request, handler.Context ?? new RequestContext());
        return builder;
    }

    public static PipelineBuilder<TRequest> Create<TRequest>(RequestHandlerAsync<TRequest> handler, TRequest request)
        where TRequest : class, IRequest
    {
        var registry = new SubscriberRegistry();
        registry.Add(typeof(TRequest), handler.GetType());
        var builder = new PipelineBuilder<TRequest>(registry,
            new SimpleHandlerFactoryAsync(_ => handler), Initializer.TestLoggerFactory);
        builder.BuildAsync(request, handler.Context ?? new RequestContext(), continueOnCapturedContext: false);
        return builder;
    }
}
