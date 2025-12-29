
using Paramore.Brighter;

namespace AnalyzerSamples
{
    public static class PublicationCreator
    {
        public static Publication GetPublicationNoRequestType()
        {
            return new PublicationSample() { 
                Subject="test"
            };
        }
        public static Publication GetPublicationWrongRequestType()
        {
            return new PublicationSample()
            {
                Subject = "test",
                RequestType = typeof(string)
            };
        }
        public static Publication GetPublicationWithRequestType()
        {
            return new PublicationSample()
            {
                Subject = "test",
                RequestType = typeof(EventSample)
            };
        } 
    }
    public class PublicationSample : Publication
    {
    }
    public class EventSample : Event
    {
        public EventSample(Id id) : base(id)
        {
        }
    }
}
