using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DataModel;

namespace GreetingsEntities
{
    [DynamoDBTable("People", LowerCamelCaseProperties = true)]
    public class Person
    {
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string Name { get; set; }
        
        [DynamoDBProperty]
        public IList<string> Greetings { get; set; } = new List<string>();

        [DynamoDBVersion]
        public int? VersionNumber { get; set; }
    }
}
