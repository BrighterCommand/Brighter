using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamoDbCustomProperties
    {
        [Fact]
        public void When_Creating_A_Table_With_Custom_Properties()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();

            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(
                new DynamoDbCreateProvisionedThroughput
                (
                    new ProvisionedThroughput {ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>
                    {
                        {
                            "GlobalSecondaryIndex",
                            new ProvisionedThroughput {ReadCapacityUnits = 10, WriteCapacityUnits = 10}
                        }
                    }
                )
            );

            //assert
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "Id" && attr.AttributeType == ScalarAttributeType.S);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "Amount" && attr.AttributeType == ScalarAttributeType.S);
        }

        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
        {
            [DynamoDBHashKey] [DynamoDBProperty] public string Id { get; set; }

             [DynamoDBProperty(typeof(MoneyTypeConverter))]
             public Money Amount { get; set; }
        }
    }

    public class Money : IEquatable<Money>
    {
        public Money(double amount, string currency)
        {
            this.Amount = amount;
            this.Currency = currency;
        }

        public double Amount { get; set; }
        public string Currency { get; set; }

        public bool Equals(Money other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Amount.Equals(other.Amount) && Currency == other.Currency;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Money)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ (Currency != null ? Currency.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Money left, Money right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Money left, Money right)
        {
            return !Equals(left, right);
        }
    }
    
        public class MoneyTypeConverter : IPropertyConverter
        {        
            private const string Amount = "amount";
            private const string Currency = "currency";
    
            public DynamoDBEntry ToEntry(object value)
            {
                var money = value as Money;
                if (money == null) throw new InvalidOperationException($"Supplied type was of type {value.GetType().Name} not DirectBooking.Application.Money");
    
                var json = new JObject(new JProperty(Amount, money.Amount), new JProperty(Currency, money.Currency));
                
                DynamoDBEntry entry = new Primitive
                {
                    Type = DynamoDBEntryType.String,
                    Value = json.ToString()
                };
    
                return entry;
    
            }
    
            public object FromEntry(DynamoDBEntry entry)
            {
                var primitive = entry as Primitive;
                if (primitive == null || !(primitive.Value is String) || string.IsNullOrEmpty((string)primitive.Value))
                    throw new ArgumentOutOfRangeException();
                
                var value = JObject.Parse(entry.AsString());
                
                var name = new Money((int)value[Amount], (string)value[Currency]);
                return name;
    
            }
     
}

}
