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

using System.Collections.Generic;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway
{
    /// <summary>
    /// Tests for <see cref="DeliveryCount.Resolve"/> — verifies R-28 (ADR 0077):
    /// a routed rejection copy keeps its stamped handled count; all other paths fall back
    /// to the normalised broker counter or the header count.
    /// </summary>
    public class DeliveryCountResolutionTests
    {
        // AC-41: "rejectionReason" (camelCase) is the discriminator (ADR 0077 §"Why the discriminator is rejectionReason")
        [Fact]
        public void When_resolving_delivery_count_for_a_routed_rejection_copy_should_keep_stamped_count()
        {
            //Arrange
            var bag = new Dictionary<string, object>
            {
                { RejectionMetadataKeyNames.RejectionReason, "DeliveryError" }   // "rejectionReason"
            };
            const int headerCount = 3;
            int? brokerCount = 1;    // first DLQ delivery; normalised would give 0

            //Act
            var result = DeliveryCount.Resolve(headerCount, brokerCount, bag);

            //Assert
            Assert.Equal(3, result);   // stamped count kept, not Normalise(1)=0
        }

        // PascalCase "RejectionReason" is the pump header key (Message.RejectionReasonHeaderName),
        // NOT the routing metadata key — bag lookup is ordinal/case-sensitive, so this is
        // not a routed copy and the normalised broker count is returned.
        [Fact]
        public void When_resolving_delivery_count_with_pascalcase_rejection_reason_should_not_treat_as_routed_copy()
        {
            //Arrange
            var bag = new Dictionary<string, object>
            {
                { Message.RejectionReasonHeaderName, "DeliveryError" }   // "RejectionReason" (PascalCase only)
            };
            const int headerCount = 5;
            int? brokerCount = 3;    // Normalise(3) = 2

            //Act
            var result = DeliveryCount.Resolve(headerCount, brokerCount, bag);

            //Assert
            Assert.Equal(2, result);   // normalised broker count, not stamped headerCount (5)
        }

        // When no broker counter is available (null, 0 or negative), fall back to the header count.
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void When_resolving_delivery_count_broker_count_absent_or_non_positive_should_return_header_count(
            int? brokerCount)
        {
            //Arrange
            var bag = new Dictionary<string, object>();   // no discriminator
            const int headerCount = 3;

            //Act
            var result = DeliveryCount.Resolve(headerCount, brokerCount, bag);

            //Assert
            Assert.Equal(headerCount, result);
        }

        // Positive broker count with no discriminator → normalised broker count.
        [Fact]
        public void When_resolving_delivery_count_no_discriminator_positive_broker_count_should_return_normalised()
        {
            //Arrange
            var bag = new Dictionary<string, object>();   // no rejectionReason key
            const int headerCount = 5;
            int? brokerCount = 3;    // Normalise(3) = 2

            //Act
            var result = DeliveryCount.Resolve(headerCount, brokerCount, bag);

            //Assert
            Assert.Equal(2, result);   // Normalise(3), not headerCount (5)
        }
    }
}
