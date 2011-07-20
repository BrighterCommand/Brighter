namespace Paramore.Domain.Common
{
    public class EmailAddress
    {
        public string Email { get; private set;}

        public EmailAddress(string email)
        {
            this.Email = email;
        }
    }
}