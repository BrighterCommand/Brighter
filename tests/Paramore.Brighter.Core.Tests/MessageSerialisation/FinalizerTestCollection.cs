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

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// The finalizer-release tests force a full GC (GC.Collect + WaitForPendingFinalizers)
// to reach an abandoned pipeline / lifetime scope's finalizer. That is a process-wide
// operation: run in parallel with another class, a concurrently live object can make
// the collection reach — or miss — the object under test, so the assertion flakes.
// Serialising both classes through this collection removes the overlap. The collection
// is Core.Tests-local because xUnit collections cannot span assemblies (the pump-deadlock
// collection with the same intent lives in Extensions.Tests).
[CollectionDefinition(Name, DisableParallelization = true)]
public class FinalizerTestCollection
{
    public const string Name = "Finalizer";
}
