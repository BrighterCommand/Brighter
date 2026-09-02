#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    internal sealed partial class TransformerFactory<TRequest>(TransformAttribute attribute, IAmAMessageTransformerFactory factory)
        where TRequest : class, IRequest
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TransformerFactory<TRequest>>();

        private readonly Type _messageType = typeof(TRequest);

        public Lease<IAmAMessageTransform> CreateMessageTransformer()
        {
            var transformerType = attribute.GetHandlerType();
            var lease = factory.Create(transformerType);
            if (lease is null)
                throw new ConfigurationException($"Could not create transformer {transformerType} from {factory}");
            try
            {
                var transformer = lease.Instance;
                if (attribute is WrapWithAttribute) transformer.InitializeWrapFromAttributeParams(attribute.InitializerParams());
                if (attribute is UnwrapWithAttribute) transformer.InitializeUnwrapFromAttributeParams(attribute.InitializerParams());
            }
            catch (Exception)
            {
                //the transformer was created but never returned to the caller, so we own it; release its
                //lease to the factory rather than leak it before letting the initialization error propagate.
                //Release/Dispose may throw (a user Release, or MS DI's sync scope Dispose on
                //netstandard2.0 for an IAsyncDisposable-only transform); log-and-swallow it so it cannot
                //mask the real initialization error being rethrown, but a repeated failure here (an
                //unreleased transform scope) is not left invisible.
                try { factory.Release(lease); }
                catch (Exception releaseException) { Log.FailedToReleaseTransformerAfterInitFailure(s_logger, releaseException); }
                throw;
            }
            return lease;
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Failed to release a transformer after its initialization failed; the initialization error is preserved and rethrown. A repeated failure here points at a transform Release or Dispose that throws, leaking its scope.")]
            public static partial void FailedToReleaseTransformerAfterInitFailure(ILogger logger, Exception exception);
        }
    }
}
