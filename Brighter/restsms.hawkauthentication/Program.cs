//Derived from: https://lbadri.wordpress.com/2013/11/04/thinktecture-identitymodel-hawk-nuget-package/


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
        static void Main(string[] args)
        {
            const string RESTMS_DOMAIN_DEFAULT = " http://localhost:3416/restms/domain/default";
            const string X_REQUEST_HEADER_TO_PROTECT = "X-Request-Header-To-Protect";

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

            var handler = new HawkValidationHandler(options);

            HttpClient client = HttpClientFactory.Create(handler);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

            var response = client.GetAsync(RESTMS_DOMAIN_DEFAULT).Result;
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);

            Console.Read();
        }
    }
}
