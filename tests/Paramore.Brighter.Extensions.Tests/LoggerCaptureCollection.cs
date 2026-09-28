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

using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory is a process-wide mutable static that
// AddConsumers/AddBrighter reassigns whenever logging is configured, and Brighter's internal loggers
// (e.g. UseInboxHandlerAsync<T>.s_logger) are static fields bound once per closed generic type, for the
// life of the process, to whichever factory was current at that moment. Under xUnit's default
// parallelism, any test in this assembly that builds a host with its own logging provider and asserts
// on captured log entries can have another concurrently-running test's log output routed into its
// capture by this shared static, independent of whether the two tests are otherwise related. Every test
// that captures log output for assertion, plus every test that deliberately logs at Warning or above
// (the inbox dedup tests), belongs in this collection so none of them race the shared static against
// each other.
[CollectionDefinition(NAME, DisableParallelization = true)]
public class LoggerCaptureCollection
{
    public const string NAME = "Brighter logger capture";
}
