using GreetingsEntities;

namespace GreetingsPorts.EntityGateway.Interfaces
{
    public interface IGreetingsEntityGateway : IDisposable
    {
        void AddPerson(Person person);
        void UpdatePerson(Person person);
        void DeletePerson(int id);
        Task<Person> GetPersonById(int id);
        Task<Person> GetPersonByName(string name);
        Task CommitChanges();
    }
}
