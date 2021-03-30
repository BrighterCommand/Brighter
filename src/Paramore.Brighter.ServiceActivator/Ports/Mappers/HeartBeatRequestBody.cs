namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartBeatRequestBody
    {
        public string Id { get;}

        public HeartBeatRequestBody(string id)
        {
            Id = id;
        }
    }
}
