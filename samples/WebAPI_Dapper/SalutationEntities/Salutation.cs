﻿using System;

namespace SalutationEntities
{
    public class Salutation
    {
        public long Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Greeting { get; set; }

        public Salutation() { /* ORM needs to create */ }

        public Salutation(string greeting)
        {
            Greeting = greeting;
        }
    }
}
