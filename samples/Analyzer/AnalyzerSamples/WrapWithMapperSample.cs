using Paramore.Brighter;
using Paramore.Brighter.Transforms.Attributes;

namespace AnalyzerSamples
{
    public class WrapWithMapperAsyncSample: IAmAMessageMapperAsync<SampleEvent>
    {
        public IRequestContext? Context { get; set ; }
       
        [Compress(0)]
        [ClaimCheck(0)]
        public async Task<SampleEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new SampleEvent(message.Id);
        }

        [Decompress(0)] 
        public async Task<Message> MapToMessageAsync(SampleEvent request, Publication publication, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new NotImplementedException();
        }
    }
    public class WrapWithMapperSample: IAmAMessageMapper<SampleEvent>
    {
        public IRequestContext? Context { get; set ; }
       
        [Compress(0)]
        [ClaimCheck(0)]
        public SampleEvent MapToRequest(Message message)
        {
            return new SampleEvent(message.Id);
        }

        [Decompress(0)] 
        public Message MapToMessage(SampleEvent request, Publication publication)
        {
            throw new NotImplementedException();
        }
    }
  

    public class SampleEvent(Id id) : Event(id)
    {
    }
}
