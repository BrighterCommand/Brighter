namespace Paramore.Domain.Common
{
    public class EmailAddress
    {
        private readonly string email = string.Empty;

        public EmailAddress(string email)
        {
            this.email = email;
        }

        public EmailAddress() {}

        public static implicit operator string(EmailAddress emailAddress)
        {
            return emailAddress.email;
        }
    }
}