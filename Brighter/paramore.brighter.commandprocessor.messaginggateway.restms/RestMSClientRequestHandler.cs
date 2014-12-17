using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Common.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using paramore.brighter.commandprocessor.messaginggateway.restms.Parsers;
using Thinktecture.IdentityModel.Hawk.Client;
using Thinktecture.IdentityModel.Hawk.Core;
using Thinktecture.IdentityModel.Hawk.Core.Helpers;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    public class RestMSClientRequestHandler : IAmAClientRequestHandler
    {
        readonly ILog logger;
        RestMSMessagingGatewayConfigurationSection configuration;

        public RestMSClientRequestHandler(ILog logger)
        {
            this.logger = logger;
            configuration = RestMSMessagingGatewayConfigurationSection.GetConfiguration();
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public Task Send(Message message)
        {
            var tcs = new TaskCompletionSource<object>();

            var options = BuildClientOptions();

            try
            {
                RestMSDomain domain= GetDefaultDomain(options);
                bool isFeedDeclared = domain.Feeds.Any(feed => feed.Name == configuration.Feed.Name);
                if (!isFeedDeclared)
                {
                    CreateFeed(configuration.Feed.Name, options);
                }


                //Query the domain
                //If our feed does not exist then
                //declare our feed
                //Send a message to it
            }
            catch (HttpRequestException he)
            {
                 logger.ErrorFormat("Error getting the default domain from the RestMS server: {0}", he.ToString());
                tcs.SetException(he);
                 throw;
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RestMSClientRequestHandler()
        {
            Dispose(false);
        }


        void Dispose(bool disposing)
        {
            
        }
        ClientOptions BuildClientOptions()
        {
            var credential = new Credential()
            {
                Id = configuration.RestMS.Id, 
                Algorithm = SupportedAlgorithms.SHA256,
                User = configuration.RestMS.User,
                Key = Convert.FromBase64String(configuration.RestMS.Key) 
            };

            var options = new ClientOptions()
            {
                CredentialsCallback = () => credential
            };
            return options;
        }

         RestMSDomain GetDefaultDomain(ClientOptions options)
         {
            var client = CreateClient(options);

            var response = client.GetAsync(configuration.RestMS.Uri).Result;
            response.EnsureSuccessStatusCode();
            var entityBody = response.Content.ReadAsStringAsync().Result;
            RestMSDomain domainObject;
            if (!ResultParser.TryParse(entityBody, out domainObject))
            {
                var errorString = string.Format("Could not parse entity body as a domain => {0}", entityBody);
                logger.ErrorFormat(errorString);
                throw new ResultParserException(errorString);
            }
            return domainObject;
         }

        void CreateFeed(string name, ClientOptions options)
        {
            var client = CreateClient(options);
            var feed = new RestMSFeed
            {
                Name = name,
                Type = "Default",
                Title = name
            };
            var response = client.PostAsXmlAsync(configuration.RestMS.Uri.AbsoluteUri, feed);
        }

        HttpClient CreateClient(ClientOptions options)
        {
            var handler = new HawkValidationHandler(options);
            var client = HttpClientFactory.Create(handler);
            client.Timeout = new TimeSpan(500);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }

    }
}