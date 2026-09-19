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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A sync handler for <see cref="PlaceSingletonOrder"/> that takes a container-<c>Singleton</c>
/// <see cref="ISingletonDependency"/> - never a <c>Scoped</c> one, which would make a <c>Singleton</c>
/// handler a captive-dependency graph - and records itself and that dependency into an injected
/// <see cref="SingletonOrderRecorder"/> on every <c>Handle</c>.
/// </summary>
public sealed class PlaceSingletonOrderHandler : RequestHandler<PlaceSingletonOrder>
{
    private readonly ISingletonDependency _dependency;
    private readonly SingletonOrderRecorder _recorder;

    public PlaceSingletonOrderHandler(ISingletonDependency dependency, SingletonOrderRecorder recorder)
    {
        _dependency = dependency;
        _recorder = recorder;
    }

    public override PlaceSingletonOrder Handle(PlaceSingletonOrder command)
    {
        _recorder.Record(this, _dependency);
        return base.Handle(command);
    }
}
