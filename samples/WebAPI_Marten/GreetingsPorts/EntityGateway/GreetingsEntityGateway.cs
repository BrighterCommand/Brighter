using GreetingsEntities;
using GreetingsPorts.EntityGateway.Interfaces;
using Marten;

namespace GreetingsPorts.EntityGateway
{
    public class GreetingsEntityGateway : IGreetingsEntityGateway
    {
        private readonly IDocumentSession session;

        public GreetingsEntityGateway(IDocumentSession session)
        {
            this.session = session;
        }

        public void AddPerson(Person person)
        {
            session.Insert(person);
        }

        public void UpdatePerson(Person person)
        {
            session.Update(person);
        }

        public void DeletePerson(int id)
        {
            session.Delete<Person>(id);
        }

        public Task<Person> GetPersonById(int id)
        {
            return session.LoadAsync<Person>(id);
        }

        public Task<Person> GetPersonByName(string name)
        {
            return session.Query<Person>()
                .SingleOrDefaultAsync(x => x.Name == name);
        }

        public async Task CommitChanges()
        {
            await session.SaveChangesAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                session.Dispose();
            }
        }
    }
}
