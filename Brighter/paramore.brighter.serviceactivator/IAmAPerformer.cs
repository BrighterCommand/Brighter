using System.Threading.Tasks;

namespace paramore.brighter.serviceactivator
{
    public interface IAmAPerformer
    {
        void Stop();
        Task Run();
    }
}