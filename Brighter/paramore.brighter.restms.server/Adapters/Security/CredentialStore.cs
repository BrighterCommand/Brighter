using System.Collections.Generic;
using Thinktecture.IdentityModel.Hawk.Core;

namespace paramore.brighter.restms.server.Adapters.Security
{
    public class CredentialStore: IAmACredentialStore
    {
        List<Credential> credentials = new List<Credential>();

        #region Implementation of IAmACredentialStore

        public void Add(Credential credential)
        {
            credentials.Add(credential);
        }

        public IEnumerable<Credential> Credentials
        {
            get { return credentials; }
        }

        #endregion
    }

    public interface IAmACredentialStore
    {
        void Add(Credential credential);
        IEnumerable<Credential> Credentials { get; }
    }
}
