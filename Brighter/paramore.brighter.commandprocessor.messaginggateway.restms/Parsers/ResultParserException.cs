using System;

namespace paramore.brighter.commandprocessor.messaginggateway.restms.Parsers
{
    internal class ResultParserException : Exception
    {
        public ResultParserException(){}

        public ResultParserException(string message): base(message){}

        public ResultParserException(string message, Exception innerException) : base(message, innerException){}
    }
}