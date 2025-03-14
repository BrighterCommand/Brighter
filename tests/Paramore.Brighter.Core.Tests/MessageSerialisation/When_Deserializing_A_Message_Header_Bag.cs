using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class BagHelper 
    {
        //We use a Dictionary<string, object> for a header. System.Json.Text will serialize this as expected
        //but when deserializing it will return a a Dictionary<string, object> where the object is a JsonElement
        //not a primitive/reference type. 
        //The goal here is to convert back
        [Fact]
         public void When_deserializing_a_message_header_bag()
         {
             //Arrange
             var header = new MessageHeader(
                 messageId: Guid.NewGuid().ToString(),
                 topic: new RoutingKey("MyTopic"),
                 messageType: MessageType.MT_EVENT,
                 timeStamp: DateTime.UtcNow,
                 correlationId: Guid.NewGuid().ToString()
                );

             var myGuid = Guid.NewGuid();
             var expectedBag = new Dictionary<string, object>
             {
                 {"myStringKey", "A string value"},
                 {"myDateTimeKey", DateTime.UtcNow},
                 {"myIntegerKey", 123},
                 {"myDecimalKey", 123.56},
                 {"myBooleanKeyTrue", true},
                 {"myBooleanKey", false},
                 {"myGuid", myGuid},
                 {"myArrayKey", new int[]{1,2,3,4,}}
             };

             foreach (var key in expectedBag.Keys)
             {
                header.Bag.Add(key, expectedBag[key]);
             }

             var json = JsonSerializer.Serialize(header, JsonSerialisationOptions.Options);
             
             //Act
             MessageHeader deserializedHeader = JsonSerializer.Deserialize<MessageHeader>(json, JsonSerialisationOptions.Options);
             //fix the headers to pass
             
             //Assert
             foreach (var key in expectedBag.Keys)
             {
                 if (key != "myArrayKey")
                 {
                     var expected = expectedBag[key];
                     var actual = deserializedHeader!.Bag[key];
                     
                     Assert.Equivalent(expected, actual);
                 }
                 if (key == "myArrayKey")
                 {
                     var expectedVals = (int[])expectedBag[key];
                     var providedVals = (List<object>)deserializedHeader.Bag[key];
                     
                     for (int i = 0; i < 4; i++)
                     {
                         int actual = Convert.ToInt32(providedVals[i]);
                         int expected = expectedVals[i]; 
                         Assert.Equal(expected, actual);
                     }
                 }
             }

         }
    }
}
