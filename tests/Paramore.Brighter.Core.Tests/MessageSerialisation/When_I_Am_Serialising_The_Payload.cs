﻿using System;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageValueSerializationTests 
{
    
    
    [Fact]
    public void When_I_serialise_a_vanilla_payload_as_a_utf8_string()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        
        var serBody = new MessageBody(body, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        
        //act
        var serBodyValue = serBody.Value;
        
        var desBody = new MessageBody(serBodyValue, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);
        serBody.Bytes.Should().Equal(desBody.Bytes);
        serBody.Value.Should().Be(desBody.Value);

    }
    
    [Fact]
    public void When_I_serialise_a_vanilla_payload_as_a_base64_string()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        
        var serBody = new MessageBody(body, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        
        //act
        var serBodyValue = serBody.ToCharacterEncodedString(CharacterEncoding.Base64);
        
        var desBody = new MessageBody(serBodyValue, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.Base64);
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.Base64);
        serBody.Bytes.Should().Equal(desBody.Bytes);
        serBody.Value.Should().Be(desBody.ToCharacterEncodedString(CharacterEncoding.UTF8));

    }
    
    [Fact]
    public void When_I_serialise_a_raw_payload_as_binary()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var serBody = new MessageBody(Encoding.UTF8.GetBytes(body), MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Raw);
        
        //act
        var desBody = new MessageBody(serBody.Bytes, MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Raw);
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.Raw);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.Raw);  
        serBody.Bytes.Should().Equal(desBody.Bytes); 

    }
    
    [Fact]
    public void When_I_serialise_a_raw_payload_as_a_base64_string()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var serBody = new MessageBody(Encoding.UTF8.GetBytes(body), MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Raw);
        
        //act
        //Ask for the bytes as a base 64 string
        var bodyAsString = serBody.ToCharacterEncodedString(CharacterEncoding.Base64); 
        
        var desBody = new MessageBody(serBody.Bytes, MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Base64);
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.Raw);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.Base64);  
        serBody.Bytes.Should().Equal(desBody.Bytes); 

    }
    
    [Fact]
    public void When_I_try_to_serialise_a_raw_payload_as_a_string()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var serBody = new MessageBody(Encoding.UTF8.GetBytes(body), MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Raw);
        
        //act
        //If we are raw, we get the bytes as a base64 encoded string
        var bodyAsString = serBody.Value;
        
        var desBody = new MessageBody(bodyAsString, MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Base64);
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.Raw);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.Base64);  
        serBody.Bytes.Should().Equal(desBody.Bytes); 
    }
    
    
    [Fact]
    public void When_I_serialise_a_kafka_payload_as_binary()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var id = 1234;
        //Emulate Kafka SerDes that puts header bytes into the payload
        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var magicByte = new byte[] { 0 };
        var schemaId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(id));
        var payload = magicByte.Concat(schemaId).ToArray();
        var serdesBody = payload.Concat(Encoding.ASCII.GetBytes(body)).ToArray();
        
        var serBody = new MessageBody(serdesBody, MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Raw);
        
        //act
        //Ask for the value back as a Base64 encoded string
        var bodyAsString = serBody.ToCharacterEncodedString(CharacterEncoding.Base64);
        
        //will be base64 encoded when read back
        var desBody = new MessageBody(bodyAsString, MediaTypeNames.Application.Octet, characterEncoding: CharacterEncoding.Base64);
        var retrievedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(desBody.Bytes.Skip(1).Take(4).ToArray()));
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.Raw);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.Base64);  
        serBody.Bytes.Should().Equal(desBody.Bytes); 
        retrievedSchemaId.Should().Be(id);

    }
    
    [Fact]
    public void When_I_serialise_a_utf8_kafka_payload_as_bytes()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var id = 1234;
        //Emulate Kafka SerDes that puts header bytes into the payload
        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var magicByte = new byte[] { 0 };
        var schemaId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(id));
        var payload = magicByte.Concat(schemaId).ToArray();
        var serdesBody = payload.Concat(Encoding.ASCII.GetBytes(body)).ToArray();
        
        var serBody = new MessageBody(serdesBody, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        
        //act
        var bodyAsBytes = serBody.Bytes;    //Transfer as bytes
        
        var desBody = new MessageBody(bodyAsBytes, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        var retrievedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(desBody.Bytes.Skip(1).Take(4).ToArray()));
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);
        serBody.Bytes.Should().Equal(desBody.Bytes);
        serBody.Value.Should().Be(desBody.Value);
        retrievedSchemaId.Should().Be(id);

    }
    
    [Fact]
    public void When_I_try_to_serialise_a_utf8_kafka_payload_as_a_utf8_string()
    {
        //arrange
        var request = new MyTransformableCommand
        {
            Value = "Hello World"
        };

        var id = 1234;
        //Emulate Kafka SerDes that puts header bytes into the payload
        var body = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options);
        var magicByte = new byte[] { 0 };
        var schemaId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(id));
        var payload = magicByte.Concat(schemaId).ToArray();
        var serdesBody = payload.Concat(Encoding.ASCII.GetBytes(body)).ToArray();
        
        var serBody = new MessageBody(serdesBody, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        
        //act
        var bodyAsBytes = serBody.Value;   //Transfer as utf8 string fails for Kafka
        
        var desBody = new MessageBody(bodyAsBytes, MediaTypeNames.Application.Json, characterEncoding: CharacterEncoding.UTF8);
        var retrievedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(desBody.Bytes.Skip(1).Take(4).ToArray()));
        
        //assert
        serBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);    
        desBody.CharacterEncoding.Should().Be(CharacterEncoding.UTF8);
        
        //Note the issue here, that the UTF conversion means that we do not get back the same bytes
        serBody.Bytes.Should().NotEqual(desBody.Bytes);

    }
}

