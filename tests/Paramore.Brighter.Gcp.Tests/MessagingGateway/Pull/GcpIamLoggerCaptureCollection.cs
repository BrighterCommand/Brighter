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

using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Defines a dedicated xUnit collection for tests that replace
/// <see cref="Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory"/> with a
/// <see cref="Paramore.Brighter.Gcp.Tests.TestDoubles.CapturingLoggerFactory"/> and assert on
/// exactly two IAM Warning entries. <c>DisableParallelization = true</c> ensures no other GCP
/// test collection runs in parallel while these tests hold the global logger factory, preventing
/// stray Warning entries from polluting the captured buffer.
/// </summary>
[CollectionDefinition("GcpIamLoggerCapture", DisableParallelization = true)]
public sealed class GcpIamLoggerCaptureCollection;
