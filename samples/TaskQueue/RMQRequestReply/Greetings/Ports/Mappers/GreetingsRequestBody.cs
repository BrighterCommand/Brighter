namespace Greetings.Ports.Mappers
{
    public class GreetingsRequestBody
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }

        public GreetingsRequestBody (string id, string name, string language)
        {
            Id = id;
            Name = name;
            Language = language;
        }
    }
}
