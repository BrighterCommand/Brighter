using Amazon.Runtime;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocumentsAndFolders.Sqs.Core.Ports.Events;
using DocumentsAndFolders.Sqs.Core.Ports.Mappers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.AWSSQS;
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


            var awsCredentials = new StoredProfileAWSCredentials();
            
            var sqsMessageProducer = new SqsMessageProducer(awsCredentials);
            var processor = new CommandProcessor(
                null,
                policyRegistry,
                messageMapperRegistry,
                new FakeMessageStore(),
                sqsMessageProducer
                );

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
        private Dictionary<string, string> _parameters;

        // Constructor
        public Arguments(string[] Args)
        {
            _parameters = new Dictionary<string, string>();
            var spliter = new Regex(@"^-{1,2}|^/|=|:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var remover = new Regex(@"^['""]?(.*?)['""]?$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            string parameter = null;

            // Valid parameters forms:
            // {-,/,--}param{ ,=,:}((",')value(",'))
            // Examples: 
            // -param1 value1 --param2 /param3:"Test-:-work" 
            //   /param4=happy -param5 '--=nice=--'
            foreach (var txt in Args)
            {
                // Look for new parameters (-,/ or --) and a
                // possible enclosed value (=,:)
                var parts = spliter.Split(txt, 3);

                switch (parts.Length)
                {
                    // Found a value (for the last parameter 
                    // found (space separator))
                    case 1:
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                            {
                                parts[0] =
                                    remover.Replace(parts[0], "$1");

                                _parameters.Add(parameter, parts[0]);
                            }
                            parameter = null;
                        }
                        // else Error: no parameter waiting for a value (skipped)
                        break;

                    // Found just a parameter
                    case 2:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                                _parameters.Add(parameter, "true");
                        }
                        parameter = parts[1];
                        break;

                    // Parameter with enclosed value
                    case 3:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                                _parameters.Add(parameter, "true");
                        }

                        parameter = parts[1];

                        // Remove possible enclosing characters (",')
                        if (!_parameters.ContainsKey(parameter))
                        {
                            parts[2] = remover.Replace(parts[2], "$1");
                            _parameters.Add(parameter, parts[2]);
                        }

                        parameter = null;
                        break;
                }
            }
            // In case a parameter is still waiting
            if (parameter != null)
            {
                if (!_parameters.ContainsKey(parameter))
                    _parameters.Add(parameter, "true");
            }
        }

        // Retrieve a parameter value if it exists 
        // (overriding C# indexer property)
        public string this[string Param] => (_parameters[Param]);
    }
}
