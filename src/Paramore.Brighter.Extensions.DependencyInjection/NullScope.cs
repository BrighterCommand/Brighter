using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class NullScope : IServiceScope
    {
        public void Dispose()
        {
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
