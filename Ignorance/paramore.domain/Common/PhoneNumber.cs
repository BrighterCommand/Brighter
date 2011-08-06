namespace Paramore.Domain.Common
{
    public class PhoneNumber
    {
        private string number;

        public PhoneNumber(string number)
        {
            this.number = number;
        }

        public static implicit operator string(PhoneNumber phoneNumber)
        {
            return phoneNumber.number;
        }
    }
}