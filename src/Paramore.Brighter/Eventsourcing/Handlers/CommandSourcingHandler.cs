#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.Eventsourcing.Exceptions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Eventsourcing.Handlers
{
    /// <summary>
    /// Class CommandSourcingHandler.
    /// Used with the Event Sourcing pattern that stores the commands that we send to handlers for replay. 
    /// http://martinfowler.com/eaaDev/EventSourcing.html
    /// Note that without a mechanism to prevent raising events from commands the danger of replay is that if events are raised downstream that are not idempotent then replay can have undesired effects.
    /// A mitigation is not to record inputs, only changes of state to the model and replay those. Of course it is possible that publishing events is desirable.
    /// To distinguish the variants the approach is now properly Event Sourcing (because it captures events that occur because of the Command) and the Fowler
    /// approach is typically called Command Sourcing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandSourcingHandler<T> : RequestHandler<T> where T: class, IRequest
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<CommandSourcingHandler<T>>);

        private readonly IAmACommandStore _commandStore;
        private bool _onceOnly;
        private string _contextKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}" /> class.
        /// </summary>
        /// <param name="commandStore">The store for commands that pass into the system</param>
        public CommandSourcingHandler(IAmACommandStore commandStore)
        {
            _commandStore = commandStore;
        }
        
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            _onceOnly = (bool) initializerList[0];
            _contextKey = (string)initializerList[1];
            base.InitializeFromAttributeParams(initializerList);
        }

        /// <summary>
        /// Logs the command we received to the command store.
        /// If the Once Only flag is set, it will reject commands that it has already seen from the pipeline
        /// </summary>
        /// <param name="command">The command that we want to store.</param>
        /// <returns>The parameter to allow request handlers to be chained together in a pipeline</returns>
        public override T Handle(T command) 
        {
            if (_onceOnly)
            {
                 _logger.Value.DebugFormat("Checking if command {0} has already been seen", command.Id);
                if (_commandStore.Exists<T>(command.Id, _contextKey))
                {
                    _logger.Value.DebugFormat("Command {0} has already been seen", command.Id);
                    throw new OnceOnlyException($"A command with id {command.Id} has already been handled");
                }

            }
            
            T handledCommand = base.Handle(command);

            _logger.Value.DebugFormat("Writing command {0} to the Command Store", command.Id);

            _commandStore.Add(command, _contextKey);

            return handledCommand;
        }

    }
}
