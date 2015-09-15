using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DocumentsAndFolders.Sqs.Adapters.ServiceHost;
using DocumentsAndFolders.Sqs.Ports.Events;

using Greetings.Adapters.ServiceHost;
using Greetings.Ports.CommandHandlers;
using Greetings.Ports.Mappers;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

using Polly;

using TinyIoC;

namespace DocumentsAndFolders.Sqs.EventsGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Arguments cmdline = new Arguments(args);

            var eventType = cmdline["eventType"];
            int eventCount;
            if (!int.TryParse(cmdline["eventCount"], out eventCount)) eventCount = 10;
            int firstEventId;
            if (!int.TryParse(cmdline["firstEventId"], out firstEventId)) firstEventId = 1;
            int documentPerFolder;
            if (!int.TryParse(cmdline["documentPerFolder"], out documentPerFolder)) documentPerFolder = 1;
            int firstFolderId;
            if (!int.TryParse(cmdline["firstFolderId"], out firstFolderId)) firstFolderId = 1;

            int eventId = firstEventId;
            int folderId = firstFolderId;

            var processor = GetCommandProcessor();
            
            for (var i = 0; i < eventCount; i++)
            {
                switch (eventType)
                {
                    case "DocumentCreatedEvent":
                        var documentCreatedEvent = new DocumentCreatedEvent(Guid.NewGuid(), eventId, string.Format("Document_{0}", eventId), folderId);
                        processor.Post(documentCreatedEvent);
                        break;
                    case "DocumentUpdatedEvent":
                        var documentUpdatedEvent = new DocumentUpdatedEvent(Guid.NewGuid(), eventId, string.Format("Document_{0}", eventId), folderId);
                        processor.Post(documentUpdatedEvent);
                        break;
                    case "FolderCreatedEvent":
                        var folderCreatedEvent = new FolderCreatedEvent(Guid.NewGuid(), eventId, string.Format("Folder_{0}", eventId));
                        processor.Post(folderCreatedEvent);
                        break;
                }

                eventId++;

                if ((i + 1) % documentPerFolder == 0) folderId++;
            }
        }

        private static IAmACommandProcessor GetCommandProcessor()
        {
            var container = new TinyIoCContainer();

            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);


            //create policies
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                    {
                        TimeSpan.FromMilliseconds(50),
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(150)
                    });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };

            //create message mappers
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(FolderCreatedEvent), typeof(FolderCreatedEventMessageMapper)},
                {typeof(DocumentCreatedEvent), typeof(DocumentCreatedEventMessageMapper)},
                {typeof(DocumentUpdatedEvent), typeof(DocumentUpdatedEventMessageMapper)}
            };


            var logger = LogProvider.For<Program>();
            
            
            var sqsMessageProducer = new SqsMessageProducer(logger);
            var processor = new CommandProcessor(
                null,
                policyRegistry,
                messageMapperRegistry,
                new FakeMessageStore(),
                sqsMessageProducer,
                logger);

            return processor;
        }
    }

    public class FakeMessageStore : IAmAMessageStore<Message>
    {
        public bool MessageWasAdded { get; set; }
        public void Add(Message message, int messageStoreTimeout = -1)
        {
            MessageWasAdded = true;
        }
        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            return null;
        }
    }

    public class Arguments
    {
        // Variables
        private StringDictionary Parameters;

        // Constructor
        public Arguments(string[] Args)
        {
            Parameters = new StringDictionary();
            Regex Spliter = new Regex(@"^-{1,2}|^/|=|:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            Regex Remover = new Regex(@"^['""]?(.*?)['""]?$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            string Parameter = null;
            string[] Parts;

            // Valid parameters forms:
            // {-,/,--}param{ ,=,:}((",')value(",'))
            // Examples: 
            // -param1 value1 --param2 /param3:"Test-:-work" 
            //   /param4=happy -param5 '--=nice=--'
            foreach (string Txt in Args)
            {
                // Look for new parameters (-,/ or --) and a
                // possible enclosed value (=,:)
                Parts = Spliter.Split(Txt, 3);

                switch (Parts.Length)
                {
                    // Found a value (for the last parameter 
                    // found (space separator))
                    case 1:
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                            {
                                Parts[0] =
                                    Remover.Replace(Parts[0], "$1");

                                Parameters.Add(Parameter, Parts[0]);
                            }
                            Parameter = null;
                        }
                        // else Error: no parameter waiting for a value (skipped)
                        break;

                    // Found just a parameter
                    case 2:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                                Parameters.Add(Parameter, "true");
                        }
                        Parameter = Parts[1];
                        break;

                    // Parameter with enclosed value
                    case 3:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                                Parameters.Add(Parameter, "true");
                        }

                        Parameter = Parts[1];

                        // Remove possible enclosing characters (",')
                        if (!Parameters.ContainsKey(Parameter))
                        {
                            Parts[2] = Remover.Replace(Parts[2], "$1");
                            Parameters.Add(Parameter, Parts[2]);
                        }

                        Parameter = null;
                        break;
                }
            }
            // In case a parameter is still waiting
            if (Parameter != null)
            {
                if (!Parameters.ContainsKey(Parameter))
                    Parameters.Add(Parameter, "true");
            }
        }

        // Retrieve a parameter value if it exists 
        // (overriding C# indexer property)
        public string this[string Param]
        {
            get
            {
                return (Parameters[Param]);
            }
        }
    }
}
