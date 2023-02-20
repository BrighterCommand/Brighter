﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NJsonSchema.Annotations;

namespace Paramore.Brighter.Core.Tests.Schemas.Test_Doubles
{

    public class MyContainedType
    {
        public string IAmYetAnotherString { get; set; }
    }

    /// <summary>
    /// This is used for testing how we create and register schemas
    /// </summary>
    [JsonSchemaFlatten]
    public class MySchemaRegistryCommand : Command
    {
        /// <summary>
        /// I am an integer parameter of the command
        /// </summary>
        [NotNull]
        [Required]
        public int IAmAnInt { get; set; }

        /// <summary>
        /// I am a boolean parameter of the command
        /// </summary>
        public bool IAmABool { get; set; }

        /// <summary>
        /// I am a string parameter of the command
        /// </summary>
        [Required]
        public string IAmAString { get; set; }

        /// <summary>
        /// I am an anther sting parameter, internal and should not be on schema
        /// NOTE: you have to ignore BOTH the schema AND the property so that it does not break schema by being serialized.
        /// </summary>
        [JsonSchemaIgnore]
        [JsonIgnore]
        public string IAmAnotherString { get; set; }

        /// <summary>
        /// I am a float parameter of the command
        /// </summary>
        public float IAmAFloat { get; set; }

        /// <summary>
        /// I am a double parameter of the command
        /// </summary>
        public double IAmADouble { get; set; }

        /// <summary>
        /// I am a contained type of the command
        /// </summary>
        public MyContainedType IAmAContainedType { get; set; }

        /// <summary>
        /// Constructs an instance of the command
        /// </summary>
        public MySchemaRegistryCommand() : base(Guid.NewGuid()) { }
    }
}
