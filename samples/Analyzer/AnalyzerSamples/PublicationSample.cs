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
                RequestType = typeof(EventSample2)
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
        public static AnyClass GetAnyOtherClass()
        {
            return new AnyClass();
        }
    }
    public class PublicationSample : Publication
    {
    }
    public class EventSample(Id id) : Event(id)
    {
    }
    public class EventSample2
    {
    }

    public class AnyClass
    {

    }
}
