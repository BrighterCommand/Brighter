using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Analyzer.Analyzers;
using Paramore.Brighter.Analyzer.Tests.Analyzers;

namespace Paramore.Brighter.Analyzer.Test.Analyzers
{
    public class SubscriptionConstructorAnalyzerTest : BaseAnalyzerTest<SubscriptionConstructorAnalyzer>
    {
        [Fact]
        public async Task When_Initializing_Subscription_With_MessagePump()
        {

            testContext.TestCode = /* lang=c#-test */ """
            using Paramore.Brighter;
            namespace TestNamespace
            {
            var subscription = {|#0:new SubscriptionTest("name", "name", "key", messagePumpType: MessagePumpType.Reactor)|};
        
            public class SubscriptionTest : Subscription
                {
                    public SubscriptionTest(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
                    {
                    }
                }
                    }
""";

            await testContext.RunAsync();
        }

        [Fact]
        public async Task When_Initializing_Subscription_WithOut_MessagePump()
        {

            testContext.TestCode = /* lang=c#-test */ """
            using Paramore.Brighter;
            namespace TestNamespace
            {
            var subscription = {|#0:new SubscriptionTest("name", "name", "key")|};
        
            public class SubscriptionTest : Subscription
                {
                    public SubscriptionTest(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
                    {
                    }
                }
                    }
""";

            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(SubscriptionConstructorAnalyzer.MessagePumpMissingRule).WithLocation(0).WithArguments("SubscriptionTest"));
            await testContext.RunAsync();
        }
        [Fact]
        public async Task When_Initializing_SubscriptionNested_WithOut_MessagePump()
        {

            testContext.TestCode = /* lang=c#-test */ """
            using Paramore.Brighter;
            namespace TestNamespace
            {
            var subscription = {|#0:new SubscriptionTestNested("name", "name", "key")|};
        
            public class SubscriptionTest : Subscription
            {
                public SubscriptionTest(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
                {
                }
            }
            public class SubscriptionTestNested : SubscriptionTest
            {
                public SubscriptionTestNested(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
                {
                }
            }
                    }
""";

            testContext.ExpectedDiagnostics.Add(new DiagnosticResult(SubscriptionConstructorAnalyzer.MessagePumpMissingRule).WithLocation(0).WithArguments("SubscriptionTest"));
            await testContext.RunAsync();
        }

    }
}
