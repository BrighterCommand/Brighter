using GreetingsEntities;
using Marten;

namespace GreetingsPorts.EntityGateway
{
    public class GreetingsEntityGateway
    {
        public void Check()
        {
            var store = DocumentStore
                .For("host=localhost;database=marten_db;password=password;username=root");

            using var session = store.LightweightSession();
            var fakeUser = new FakeUser
            {
                Id = 1,
                Name = "test-name",
            };
            session.Store(fakeUser);
            session.SaveChanges();
        }

        public class FakeUser
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

}
