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

namespace Paramore.Brighter.Testing;

/// <summary>
/// Identifies which IAmACommandProcessor method was called.
/// </summary>
public enum CommandType
{
    /// <summary>Synchronous Send method was called.</summary>
    Send,
    /// <summary>Asynchronous SendAsync method was called.</summary>
    SendAsync,
    /// <summary>Synchronous Publish method was called.</summary>
    Publish,
    /// <summary>Asynchronous PublishAsync method was called.</summary>
    PublishAsync,
    /// <summary>Synchronous Post method was called.</summary>
    Post,
    /// <summary>Asynchronous PostAsync method was called.</summary>
    PostAsync,
    /// <summary>Synchronous DepositPost method was called.</summary>
    Deposit,
    /// <summary>Asynchronous DepositPostAsync method was called.</summary>
    DepositAsync,
    /// <summary>Synchronous ClearOutbox method was called.</summary>
    Clear,
    /// <summary>Asynchronous ClearOutboxAsync method was called.</summary>
    ClearAsync,
    /// <summary>Call method was called (request-reply pattern).</summary>
    Call,
    /// <summary>Synchronous scheduled method was called.</summary>
    Scheduler,
    /// <summary>Asynchronous scheduled method was called.</summary>
    SchedulerAsync
}
