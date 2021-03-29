namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyRequestDTO
    {
        public string Id { get; set; }
        public string RequestValue { get; set; }

        public MyRequestDTO(string id, string requestValue)
        {
            Id = id;
            RequestValue = requestValue;
        }
    }
}
