#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// What a store holds for a topic after a write.
/// </summary>
/// <param name="Topic">The topic that was written.</param>
/// <param name="PriorTripTime">The stamp the store held before the write, or null if the row was inserted or had ended.</param>
/// <param name="StoredTripTime">The stamp the store holds after the write: the later of the stored and the written stamp.</param>
/// <param name="LeftEnded">True if the row is marked ended after the write.</param>
public readonly record struct TripWriteResult(RoutingKey Topic, DateTimeOffset? PriorTripTime, DateTimeOffset StoredTripTime, bool LeftEnded);
