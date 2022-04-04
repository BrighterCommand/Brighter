using System;
using System.Linq;
using System.Threading.Tasks;
using GreetingsEntities;
using Marten;

namespace GreetingsPorts.EntityGateway
{
    public class GreetingsEntityGateway : IDisposable
    {
        private readonly IDocumentSession session;

        public GreetingsEntityGateway(IDocumentSession session)
        {
            this.session = session;
        }

        public void Add(Person person)
        {
            session.Insert(person);
        }
        
        public Task<Greeting> Get(int id)
        {
            return session.LoadAsync<Greeting>(id);
        }

        public async Task CommitChanges()
        {
            await session.SaveChangesAsync();
        }

        public void Dispose()
        {
            session.Dispose();
        }
    }

}
