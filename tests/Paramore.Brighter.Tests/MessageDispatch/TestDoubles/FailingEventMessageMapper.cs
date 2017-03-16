using System;

namespace Paramore.Brighter.Tests.MessageDispatch.TestDoubles
{
    internal class FailingEventMessageMapper : IAmAMessageMapper<MyFailingMapperEvent>
    {
        public Message MapToMessage(MyFailingMapperEvent request)
        {
            throw new NotImplementedException();
        }

        public MyFailingMapperEvent MapToRequest(Message message)
        {
            throw new Exception();
            //return JsonConvert.DeserializeObject<MyFailingMapperEvent>(message.Body.Value);
        }
    }

    internal class MyFailingMapperEvent : IRequest
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Guid Id { get; set; }
        public string MissingStringField { get; set; }
        public int MissingIntField { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public MyFailingMapperEvent()
        {
            Id = new Guid();
        }
    }
}