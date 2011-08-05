using System;
using Paramore.Infrastructure.Raven;

// If we want to encapsulate and still use RavenDb's LINQ query support we need to seperate the document structure from the entity
// This goes 'over-the-wire' as the body of an HTTP Request to RavenDb anyway
// It's easy to populate test code, as we can just get builders to crank DTOs into RavenDb
// But there is some friction around exposing a DTO and having to deal with it whenever we alter state
// see http://codeofrob.com/archive/2010/10/04/working-with-ravendb-documents-entities-the-debate.aspx for Rob Ashton's examination of the tradeoffs with RavenDB around public-private state
// We don't quite follow the same mechanism i.e. we convert back and forth instead of treating the DTO as the representation of internal state

namespace Paramore.Infrastructure.Domain
{
    public interface IEntity<out T> where T : IAmADataObject
    {
        Id Id { get; }
        T ToDTO();
    }
}