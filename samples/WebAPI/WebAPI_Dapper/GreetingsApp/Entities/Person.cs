using System;
using System.Collections.Generic;

namespace GreetingsApp.Entities;

public class Person
{
    public DateTime TimeStamp { get; set; }
    public int Id { get; set; }
    public string? Name { get; set; }
    public IList<Greeting> Greetings { get; set; } = new List<Greeting>();
    
    public Person()
    {
        /*Required for Dapper*/
    }

    public Person(string name)
    {
        Name = name;
    }

    public Person(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
