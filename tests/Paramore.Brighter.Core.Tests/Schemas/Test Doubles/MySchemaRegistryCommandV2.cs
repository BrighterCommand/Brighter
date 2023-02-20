using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NJsonSchema.Annotations;

namespace Paramore.Brighter.Core.Tests.Schemas.Test_Doubles;

/// <summary>
/// This is used for testing how we create and register schemas
/// </summary>
[JsonSchemaFlatten]
public class MySchemaRegistryCommandV2 : Command
{
    /// <summary>
    /// I am an integer parameter of the command
    /// </summary>
    [NotNull]
    [Required]
    public int AnInt { get; set; }
    
    /// <summary>
    /// I am a boolean parameter of the command
    /// </summary>
    public bool ABool { get; set; }
    
    /// <summary>
    /// I am a float parameter of the command
    /// </summary>
    public float AFloat { get; set; }
    
    /// <summary>
    /// I am a double parameter of the command
    /// </summary>
    public double ADouble { get; set; }
    
    /// <summary>
    /// Constructs an instance of the command
    /// </summary>
    public MySchemaRegistryCommandV2() : base(Guid.NewGuid()) { }
}
