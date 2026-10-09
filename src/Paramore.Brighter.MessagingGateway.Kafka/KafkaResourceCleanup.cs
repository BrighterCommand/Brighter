#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Kafka;

internal sealed class KafkaResourceCleanup
{
    private readonly List<Exception> _exceptions = [];

    public void Try(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            _exceptions.Add(exception);
        }
    }

    public async ValueTask TryAsync(Func<ValueTask> cleanup)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _exceptions.Add(exception);
        }
    }

    public void ThrowIfFailed()
    {
        if (_exceptions.Count == 1)
            ExceptionDispatchInfo.Capture(_exceptions[0]).Throw();

        if (_exceptions.Count > 1)
            throw new AggregateException("Multiple Kafka cleanup steps failed.", _exceptions);
    }
}
