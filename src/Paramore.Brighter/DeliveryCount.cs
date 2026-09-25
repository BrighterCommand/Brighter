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

namespace Paramore.Brighter
{
    /// <summary>
    /// Normalises a broker delivery counter so that a first delivery always presents 0 (R-2/R-3).
    /// </summary>
    /// <remarks>
    /// Brokers such as SQS, Pub/Sub and RocketMQ origin their delivery counter at 1.
    /// Subtracting 1 keeps the count aligned with Brighter's zero-based <see cref="MessageHeader.HandledCount"/>.
    /// </remarks>
    public static class DeliveryCount
    {
        /// <summary>
        /// Maps a raw broker delivery counter to the zero-based count Brighter presents to the pump.
        /// </summary>
        /// <param name="brokerCount">
        /// The raw counter supplied by the broker, or <c>null</c> / <c>0</c> when the broker does not populate it
        /// (e.g. Pub/Sub without a <c>DeadLetterPolicy</c>).
        /// </param>
        /// <returns>
        /// <c>brokerCount - 1</c> when <paramref name="brokerCount"/> is greater than 1; <c>0</c> otherwise.
        /// The result is never negative.
        /// </returns>
        public static int Normalise(int? brokerCount) => brokerCount is > 1 ? brokerCount.Value - 1 : 0;
    }
}
