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
        
        public void Update(Person person)
        {
            session.Update(person);
        }

        public Task<Person> GetById(int id)
        {
            return session.LoadAsync<Person>(id);
        }

        public Task<Person> GetByName(string name)
        {
            return session.Query<Person>().SingleOrDefaultAsync(x => x.Name == name);
        }

        public async Task CommitChanges()
        {
            await session.SaveChangesAsync();
        }

        // TODO: implement IDisposable correclty
        public void Dispose()
        {
            session.Dispose();
        }
    }

}
