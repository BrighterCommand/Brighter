using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DataModel;

namespace GreetingsEntities
{
    [DynamoDBTable("People")]
    public class Person
    {
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string Name { get; set; }
        
        public List<string> Greetings { get; set; } = new List<string>();

        //[DynamoDBVersion]
        //public int? VersionNumber { get; set; }
    }
}
