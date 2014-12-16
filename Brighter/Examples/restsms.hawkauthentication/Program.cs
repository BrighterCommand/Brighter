using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Thinktecture.IdentityModel.Hawk.Client;
using Thinktecture.IdentityModel.Hawk.Core;
using Thinktecture.IdentityModel.Hawk.Core.Helpers;

namespace restsms.hawkauthentication
{
    class Program
    {
        const string RESTMS_DOMAIN_DEFAULT = " http://localhost:3416/restms/domain/default";

        static void Main(string[] args)
        {

            var options = BuildClientOptions();


            GetDefaultDomain(options);
            PostNewPipe(options);

            Console.Read();
        }


        static ClientOptions BuildClientOptions()
        {
            var credential = new Credential()
            {
                Id = "dh37fgj492je",
                Algorithm = SupportedAlgorithms.SHA256,
                User = "Guest",
                Key = Convert.FromBase64String("wBgvhp1lZTr4Tb6K6+5OQa1bL9fxK7j8wBsepjqVNiQ=")
            };

            var options = new ClientOptions()
            {
                CredentialsCallback = () => credential
            };
            return options;
        }

        static void GetDefaultDomain(ClientOptions options)
        {
            var handler = new HawkValidationHandler(options);
            var client = HttpClientFactory.Create(handler);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

            var response = client.GetAsync(RESTMS_DOMAIN_DEFAULT).Result;
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);
        }

        static void PostNewPipe(ClientOptions options)
        {
            var handler = new HawkValidationHandler(options);

            var client = HttpClientFactory.Create(handler);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

            const string BODY = "<pipe xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" title=\"title\" xmlns=\"http://www.restms.org/schema/restms\"></pipe>";

            var response = client.PostAsync(RESTMS_DOMAIN_DEFAULT, new StringContent(BODY)).Result;
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);

        }
    }
}
