using System;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageMapper
{
    public class JsonMessageMapperTests
    {
        [Fact]
        public void When_mapping_an_event_to_a_message_as_json()
        {
            var requestContext = new RequestContext();

            var mapper = new JsonMessageMapper<TestedEvent>(requestContext);

            DateTime dateTime = DateTime.UtcNow;
            TestedEvent testedEvent = new TestedEvent
            {
                Message = "This is a message",
                Number = 999,
                DateNow = dateTime
            };

            requestContext.Header.CorrelationId = Guid.NewGuid();

            var message = mapper.MapToMessage(testedEvent);

            // Message checks
            Assert.Equal(testedEvent.Id, message.Id);

           // Header checks
            Assert.Equal("application/json", message.Header.ContentType);
            Assert.Equal("TestedEvent", message.Header.Topic);
            Assert.Equal(MessageType.MT_EVENT , message.Header.MessageType);
            Assert.Equal( requestContext.Header.CorrelationId , message.Header.CorrelationId);

            // Body checks
            Assert.Equal("JSON", message.Body.BodyType);
            Assert.Equal($"{{\"message\":\"{testedEvent.Message}\",\"number\":{testedEvent.Number},\"dateNow\":\"{dateTime:yyyy-MM-ddTHH:mm:ss.FFFFFFFK}\",\"id\":\"{testedEvent.Id}\"}}", message.Body.Value);
            // Why not just datetime:O well the JsonSerializer does 7 decimal places with no trailing zeros, format "O" does 7 decimal places with trailing zeros
        }


        [Fact]
        public void When_mapping_a_command_to_a_message_as_json()
        {
            var requestContext = new RequestContext();

            var mapper = new JsonMessageMapper<TestCommand>(requestContext);

            DateTime dateTime = DateTime.UtcNow;
            TestCommand testCommand = new TestCommand
            {
                Message = "This is a message",
                Number = 999,
                DateNow = dateTime
            };

            requestContext.Header.CorrelationId = Guid.NewGuid();

            var message = mapper.MapToMessage(testCommand);

            // Message checks
            Assert.Equal(testCommand.Id, message.Id);

            // Header checks
            Assert.Equal("application/json", message.Header.ContentType);
            Assert.Equal("TestCommand", message.Header.Topic);
            Assert.Equal(MessageType.MT_COMMAND , message.Header.MessageType);
            Assert.Equal( requestContext.Header.CorrelationId , message.Header.CorrelationId);

            // Body checks
            Assert.Equal("JSON", message.Body.BodyType);
            Assert.Equal($"{{\"message\":\"{testCommand.Message}\",\"number\":{testCommand.Number},\"dateNow\":\"{dateTime:yyyy-MM-ddTHH:mm:ss.FFFFFFFK}\",\"id\":\"{testCommand.Id}\"}}", message.Body.Value);
        }

        [Fact]
        public void when_mapping_to_a_command_from_json()
        {
            var requestContext = new RequestContext();
            var mapper = new JsonMessageMapper<TestCommand>(requestContext);


            var body = "{\"message\":\"This is a message\",\"number\":999,\"dateNow\":\"2019-04-09T15:06:56.7623017Z\",\"id\":\"7d9120b9-a18e-43ac-a63e-8201a43ea623\"}";
            var correlationId = Guid.NewGuid();
            var message = new Message(new MessageHeader(new Guid("7d9120b9-a18e-43ac-a63e-8201a43ea623"),"Blah",  MessageType.MT_COMMAND, correlationId: correlationId), new MessageBody(body));

            var testCommand = mapper.MapToRequest(message);


            Assert.Equal("7d9120b9-a18e-43ac-a63e-8201a43ea623", testCommand.Id.ToString());
            Assert.Equal("This is a message", testCommand.Message);
            Assert.Equal(999, testCommand.Number);
            Assert.Equal(DateTime.Parse("2019-04-09T15:06:56.7623017Z").ToUniversalTime(), testCommand.DateNow);

            Assert.Equal(correlationId,  requestContext.Header.CorrelationId);
        }

        [Fact]
        public void When_mapping_with_custom_routing_key_to_a_message()
        {
            var mapper = new JsonMessageMapper<TestCommand>(new RequestContext(), new RoutingKey("MyTestRoute"));

            var testCommand = new TestCommand();

            var message = mapper.MapToMessage(testCommand);

            Assert.Equal("MyTestRoute", message.Header.Topic);
        }

        [Fact]
        public void When_mapping_with_custom_routing_key_to_a_message2()
        {
            var mapper = new JsonMessageMapper<TestCommand>(new RequestContext(), routingKeyFunc: request =>
            {
                string topic = "TestPreAmble.";

                string name = request.GetType().Name;

                if (name.EndsWith("Command", StringComparison.InvariantCultureIgnoreCase))
                    topic = topic + name.Replace("Command", "", StringComparison.InvariantCultureIgnoreCase);
                else if (name.EndsWith("Event", StringComparison.InvariantCultureIgnoreCase))
                    topic = topic + name.Replace("Event", "", StringComparison.InvariantCultureIgnoreCase);
                else
                {
                    topic = topic + name;
                }

                return topic;
            } );

            var testCommand = new TestCommand();

            var message = mapper.MapToMessage(testCommand);

            Assert.Equal("TestPreAmble.Test", message.Header.Topic);
        }


    }

    public class TestCommand : Command
    {
        public TestCommand() : base(Guid.NewGuid())
        {
        }

        public string Message { get; set; }
        public int Number { get; set; }
        public DateTime DateNow { get; set; }
    }

    public class TestedEvent : Event
    {
        public TestedEvent() : base(Guid.NewGuid())
        {
        }

        public string Message { get; set; }
        public int Number { get; set; }
        public DateTime DateNow { get; set; }
    }
}
