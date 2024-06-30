namespace GreetingsPorts.Entities;

public class Greeting
{
    public Greeting()
    {
        /*Required by Dapperextensions*/
    }

    public Greeting(string message)
    {
        Message = message;
    }

    public Greeting(string message, Person recipient)
    {
        Message = message;
        RecipientId = recipient.Id;
    }

    public Greeting(int id, string message, Person recipient)
    {
        Id = id;
        Message = message;
        RecipientId = recipient.Id;
    }

    public long Id { get; set; }

    public string Message { get; set; }

    //public Person Recipient { get; set; }
    public long RecipientId { get; set; }

    public string Greet()
    {
        return $"{Message}!";
    }
}
