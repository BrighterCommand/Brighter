namespace Paramore.Domain.Common
{
    public class EmailAddress
    {
        private string email;

        public EmailAddress(string email)
        {
            this.email = email;
        }

        public static implicit operator string(EmailAddress emailAddress)
        {
            return emailAddress.email;
        }
    }
}