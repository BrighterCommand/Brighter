using System;
using System.IO;
using System.Net.Mime;

namespace paramore.brighter.restms.core.Model
{
    public class MessageContent : IDisposable
    {
        readonly Stream content = new MemoryStream();
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public MessageContent(ContentType contentType, TransferEncoding encoding, Stream source)
        {
            ContentType = contentType;
            Encoding = encoding;
            source.Position = 0;
            source.CopyTo(content);
            content.Position = 0;
        }

        public ContentType ContentType { get; private set; }
        public TransferEncoding Encoding { get; private set; }

        public string AsString()
        {
            content.Position = 0;
            using (var sr = new StreamReader(content))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageContent()
        {
            Dispose(false);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                content.Dispose();
            }
        }

        public MessageContent Copy()
        {
            return new MessageContent(ContentType, Encoding, content);    
        }
    }
}
