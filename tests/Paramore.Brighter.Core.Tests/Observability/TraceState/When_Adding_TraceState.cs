﻿using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Trace;

public class TraceStateTests 
{
    [Fact]
    public void When_Adding_TraceState()
    {
        // Arrange
        var traceState = new Baggage();
        
        //act
        traceState.Add("key1", "value1");
        traceState.Add("key2", "value2");
        
        // Assert
        Assert.Equal("key1=value1,key2=value2", traceState.ToString());
    }

    [Fact]
    public void When_Adding_TraceState_From_Baggage()
    {
        //Arrange
        var traceState = new Baggage();
        var baggage = "key1=value1,key2=value2";
        
        //Act
        traceState.Add("key3", "value3");
        traceState.LoadBaggage(baggage);
        
        //Assert
        Assert.Equal("key3=value3,key1=value1,key2=value2", traceState.ToString()); 
    }

    [Fact]
    public void When_Enumerating_TraceState()
    {
        //Arrange
        var traceState = new Baggage();
        traceState.Add("key1", "value1");
        traceState.Add("key2", "value2");
        
        //Act
        
        //Assert
        Assert.Collection(traceState,
            entry =>
            {
                Assert.Equal("key1", entry.Key);
                Assert.Equal("value1", entry.Value);
            },
            entry =>
            {
                Assert.Equal("key2", entry.Key);
                Assert.Equal("value2", entry.Value);
            });
    }
}
