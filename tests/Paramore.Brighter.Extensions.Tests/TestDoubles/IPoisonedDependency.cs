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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A <c>Scoped</c>-registered dependency whose <see cref="PoisonedDependency"/> implementation throws
/// from <see cref="System.IDisposable.Dispose"/>, so a container that resolved it before some other
/// constructor parameter failed still tracks it for disposal — and disposing the pipeline scope that
/// resolved it then throws too. Injected into <see cref="PoisonedScopeMapper"/> alongside
/// <see cref="IUnregisteredDependency"/>, and into <see cref="PoisonedScopeCompletingMapper"/> on its
/// own, where the pipeline completes and the scope's disposal failure surfaces on a successful
/// <c>Post</c> instead.
/// </summary>
public interface IPoisonedDependency
{
}
