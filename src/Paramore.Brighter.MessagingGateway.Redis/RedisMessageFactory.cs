using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageFactory: IDisposable
    {
        public const string EMPTY_MESSAGE = "<HEADER HEADER/><BODY BODY/>";
        private const string BEGINNING_OF_HEADER = "<HEADER ";
        private const string END_OF_HEADER = " HEADER/>";
        private const string BEGINNING_OF_BODY = "<BODY";
        private const string END_OF_BODY = "BODY/>";
        
        private StringWriter _writer;

        public RedisMessageFactory()
        {
            _writer = new StringWriter(new StringBuilder());
        }

        public string Create(Message message)
        {
           WriteHeader(message.Header);
           WriteBody(message.Body);
           return _writer.ToString();
        }

        private void WriteBody(MessageBody messageBody)
        {
            _writer.WriteLine(BEGINNING_OF_BODY); 
            
            _writer.WriteLine(messageBody.Value);
            
            _writer.WriteLine(END_OF_BODY); 
        }

        private void WriteHeader(MessageHeader messageHeader)
        {
            _writer.WriteLine(BEGINNING_OF_HEADER);
            
            _writer.WriteLine(JsonConvert.SerializeObject(messageHeader));
            
            _writer.WriteLine(END_OF_HEADER);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}