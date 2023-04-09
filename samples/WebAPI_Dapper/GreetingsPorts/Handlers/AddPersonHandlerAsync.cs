﻿using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly IUnitOfWork _uow;

        public AddPersonHandlerAsync(IUnitOfWork uow)
        {
            _uow = uow;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default)
        {
            await _uow.Database.InsertAsync<Person>(new Person(addPerson.Name));
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
