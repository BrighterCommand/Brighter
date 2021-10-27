using System;

namespace Paramore.Brighter.ServiceActivator
{
    public interface IAmACommandProcessorProvider : IDisposable
    {
        IAmACommandProcessor Get();

        void CreateScope();

        void ReleaseScope();
    }
}
