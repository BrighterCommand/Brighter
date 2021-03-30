namespace Greetings.Ports.Mappers
{
    public class GreetingsReplyBody
    {
        public string Id { get; set; }
        public string Salutation { get; set; }

        public GreetingsReplyBody(string id, string salutation)
        {
            Id = id;
            Salutation = salutation;
        }
    }
}
