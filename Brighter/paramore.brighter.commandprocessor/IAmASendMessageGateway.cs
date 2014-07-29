using System;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    public interface IAmASendMessageGateway: IDisposable
    {
        Task Send(Message message);
    }
}