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

namespace Paramore.Brighter.Core.Tests.MessagingGateway
{
    public class DeliveryCountNormalisationTests
    {
        [Theory]
        [InlineData(1, 0)]   // first broker delivery (origin 1) → presented count 0 (R-2)
        [InlineData(2, 1)]   // second delivery → 1
        [InlineData(3, 2)]   // third delivery → 2
        public void When_normalising_a_broker_delivery_counter_should_present_zero_on_first_delivery(
            int? brokerCount, int expectedCount)
        {
            //Arrange
            // brokerCount supplied by [InlineData]

            //Act
            var result = DeliveryCount.Normalise(brokerCount);

            //Assert
            Assert.Equal(expectedCount, result);
        }

        [Theory]
        [InlineData(null)]  // absent counter (Pub/Sub without DeadLetterPolicy, A-1)
        [InlineData(0)]     // unset counter
        [InlineData(-1)]    // negative input
        [InlineData(-5)]    // negative input
        public void When_normalising_a_broker_delivery_counter_absent_or_non_positive_should_present_zero(
            int? brokerCount)
        {
            //Arrange
            // brokerCount supplied by [InlineData]

            //Act
            var result = DeliveryCount.Normalise(brokerCount);

            //Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void When_normalising_any_broker_delivery_counter_result_is_never_negative(int? brokerCount)
        {
            //Arrange
            // brokerCount supplied by [InlineData]

            //Act
            var result = DeliveryCount.Normalise(brokerCount);

            //Assert
            Assert.True(result >= 0, $"Expected non-negative result for brokerCount={brokerCount}, got {result}");
        }
    }
}
