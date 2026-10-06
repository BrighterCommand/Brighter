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

using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class InMemoryInboxCausationTrackingTests
    {
        private const string CausationId = "causation-123";

        private readonly InMemoryInbox _inbox;
        private readonly MyCommand _command;
        private readonly string _contextKey;

        public InMemoryInboxCausationTrackingTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());
            _command = new MyCommand { Value = "My Test String" };
            _contextKey = "MyContextKey";
        }

        [Fact]
        public void When_adding_to_inbox_with_causation_id_should_store_and_retrieve()
        {
            //Arrange
            var requestContext = new RequestContext();
            requestContext.Bag[RequestContextBagNames.CausationId] = CausationId;

            //Act
            _inbox.Add(_command, _contextKey, requestContext);
            var storedCausationId = ((IAmACausationTrackingInbox)_inbox)
                .GetCausationId(_command.Id, _contextKey, requestContext);

            //Assert
            Assert.Equal(CausationId, storedCausationId);
        }

        [Fact]
        public async Task When_adding_to_inbox_with_causation_id_should_store_and_retrieve_async()
        {
            //Arrange
            var requestContext = new RequestContext();
            requestContext.Bag[RequestContextBagNames.CausationId] = CausationId;

            //Act
            await _inbox.AddAsync(_command, _contextKey, requestContext);
            var storedCausationId = await ((IAmACausationTrackingInbox)_inbox)
                .GetCausationIdAsync(_command.Id, _contextKey, requestContext);

            //Assert
            Assert.Equal(CausationId, storedCausationId);
        }

        [Fact]
        public void When_adding_to_inbox_without_causation_id_should_return_null()
        {
            //Arrange — no CausationId placed in the context bag
            var requestContext = new RequestContext();

            //Act
            _inbox.Add(_command, _contextKey, requestContext);
            var storedCausationId = ((IAmACausationTrackingInbox)_inbox)
                .GetCausationId(_command.Id, _contextKey, requestContext);

            //Assert
            Assert.Null(storedCausationId);
        }

        [Fact]
        public void When_asking_in_memory_inbox_if_it_supports_causation_tracking_should_be_true()
        {
            //Act
            var supportsCausationTracking = ((IAmACausationTrackingInbox)_inbox).SupportsCausationTracking();

            //Assert
            Assert.True(supportsCausationTracking);
        }
    }
}
