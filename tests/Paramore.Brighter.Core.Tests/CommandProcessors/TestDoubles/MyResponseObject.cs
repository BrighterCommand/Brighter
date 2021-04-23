namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyResponseObject
    {
        public string Id { get; set; }
        public string ReplyValue { get; set; }

        public MyResponseObject(string id, string replyValue)
        {
            Id = id;
            ReplyValue = replyValue;
        }
    }
}
