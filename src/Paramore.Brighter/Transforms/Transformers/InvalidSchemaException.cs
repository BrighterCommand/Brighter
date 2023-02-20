using System;

namespace Paramore.Brighter.Transforms.Transformers
{
    public class InvalidSchemaException : Exception
    {
        public InvalidSchemaException() { }
        public InvalidSchemaException(string message) : base(message) { }
        public InvalidSchemaException(string message, Exception innerException) : base(message, innerException) { }
    }
}
