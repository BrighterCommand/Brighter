using System.Collections.Generic;

namespace GreetingsApp.Responses;

public class FindPersonsGreetings
{
    public string? Name { get; set; }
    public IEnumerable<Salutation>? Greetings { get; set; }
}

public class Salutation
{
    public Salutation(string words)
    {
        Words = words;
    }

    public string Words { get; set; }
}
